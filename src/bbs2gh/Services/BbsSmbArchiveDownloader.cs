using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using OctoshiftCLI.Extensions;
using SMBLibrary;
using SMBLibrary.Client;
using FileAttributes = SMBLibrary.FileAttributes;

namespace OctoshiftCLI.BbsToGithub.Services;

public sealed class BbsSmbArchiveDownloader : IBbsArchiveDownloader
{
    public const string DEFAULT_BBS_SHARED_HOME_DIRECTORY = "c$\\atlassian\\applicationdata\\bitbucket\\shared";
    private const int DOWNLOAD_PROGRESS_REPORT_INTERVAL_IN_SECONDS = 10;

    private readonly ISMBClient _smbClient;
    private readonly OctoLogger _log;
    private readonly FileSystemProvider _fileSystemProvider;
    private readonly string _host;
    private readonly string _smbUser;
    private readonly string _smbPassword;
    private readonly string _domainName;
    private DateTime _nextProgressReport;

    public BbsSmbArchiveDownloader(OctoLogger log, FileSystemProvider fileSystemProvider, string host, string smbUser, string smbPassword, string domainName = null)
        : this(log, fileSystemProvider, new SMB2Client(), host, smbUser, smbPassword, domainName)
    {
    }

    internal BbsSmbArchiveDownloader(
        OctoLogger log,
        FileSystemProvider fileSystemProvider,
        ISMBClient smbClient,
        string host,
        string smbUser,
        string smbPassword,
        string domainName = null)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _fileSystemProvider = fileSystemProvider ?? throw new ArgumentNullException(nameof(fileSystemProvider));
        _smbClient = smbClient ?? throw new ArgumentNullException(nameof(smbClient));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _smbUser = smbUser;
        _smbPassword = smbPassword;
        _domainName = domainName;
    }

    public string BbsSharedHomeDirectory { get; init; } = DEFAULT_BBS_SHARED_HOME_DIRECTORY;

    public string GetSourceExportArchiveAbsolutePath(long exportJobId) => Path.Join(BbsSharedHomeDirectory ?? DEFAULT_BBS_SHARED_HOME_DIRECTORY,
        IBbsArchiveDownloader.GetSourceExportArchiveRelativePath(exportJobId)).ToWindowsPath();

    public async Task<string> Download(long exportJobId, string targetDirectory = IBbsArchiveDownloader.DEFAULT_TARGET_DIRECTORY)
    {
        _nextProgressReport = DateTime.Now;

        ISMBFileStore fileStore = null;
        object sourceExportArchiveHandle = null;

        var sourceExportArchiveFullPath = GetSourceExportArchiveAbsolutePath(exportJobId);
        var share = sourceExportArchiveFullPath[..sourceExportArchiveFullPath.IndexOf("\\", StringComparison.Ordinal)];
        var sourceExportArchivePathAfterShare = sourceExportArchiveFullPath[(sourceExportArchiveFullPath.IndexOf("\\", StringComparison.Ordinal) + 1)..];

        var targetExportArchiveFullPath =
            Path.Join(targetDirectory ?? IBbsArchiveDownloader.DEFAULT_TARGET_DIRECTORY, IBbsArchiveDownloader.GetExportArchiveFileName(exportJobId)).ToUnixPath();

        await using var targetExportArchive = OpenWriteTargetExportArchive(targetExportArchiveFullPath);

        try
        {
            ConnectToHost();
            Login();

            fileStore = CreateSmbFileStore(share);
            sourceExportArchiveHandle = CreateFileHandle(fileStore, sourceExportArchivePathAfterShare);
            var sourceExportArchiveSize = GetFileSize(fileStore, sourceExportArchiveHandle);

            long bytesRead = 0;
            while (true)
            {
                var status = fileStore.ReadFile(out var data, sourceExportArchiveHandle, bytesRead, (int)_smbClient.MaxReadSize);

                if (IsEndOfFileStatus(status) || data.Length == 0)
                {
                    break;
                }

                if (!IsSuccessStatus(status))
                {
                    throw new OctoshiftCliException($"Failed to read from source export archive \"{sourceExportArchiveFullPath}\" (Status Code: {status}).");
                }

                bytesRead += data.Length;
                await _fileSystemProvider.WriteAsync(targetExportArchive, data);

                LogProgress(bytesRead, sourceExportArchiveSize);
            }

            return targetExportArchiveFullPath;
        }
        finally
        {
            if (sourceExportArchiveHandle != null)
            {
                fileStore?.CloseFile(sourceExportArchiveHandle);
            }

            fileStore?.Disconnect();
            _smbClient.Logoff();
            _smbClient.Disconnect();
        }
    }

    private void LogProgress(long downloadedBytes, long? totalBytes)
    {
        if (DateTime.Now < _nextProgressReport)
        {
            return;
        }

        var totalProgressMessage = totalBytes.HasValue
            ? $" out of {GetLogFriendlySize(totalBytes.Value)} ({GetPercentage(downloadedBytes, totalBytes.Value)})"
            : "";
        _log.LogInformation($"Download archive in progress, {GetLogFriendlySize(downloadedBytes)}{totalProgressMessage} completed...");

        _nextProgressReport = _nextProgressReport.AddSeconds(DOWNLOAD_PROGRESS_REPORT_INTERVAL_IN_SECONDS);
    }

    private string GetPercentage(long downloadedBytes, long totalBytes)
    {
        if (totalBytes is 0L)
        {
            return "unknown%";
        }

        var percentage = (int)(downloadedBytes * 100D / totalBytes);
        return $"{percentage}%";
    }

    private string GetLogFriendlySize(long size)
    {
        const int kilobyte = 1024;
        const int megabyte = 1024 * kilobyte;
        const int gigabyte = 1024 * megabyte;

        return size switch
        {
            < kilobyte => $"{size:n0} bytes",
            < megabyte => $"{size / (double)kilobyte:n0} KB",
            < gigabyte => $"{size / (double)megabyte:n0} MB",
            _ => $"{size / (double)gigabyte:n2} GB"
        };
    }

    private FileStream OpenWriteTargetExportArchive(string targetExportArchiveFullPath)
    {
        if (_fileSystemProvider.FileExists(targetExportArchiveFullPath))
        {
            throw new OctoshiftCliException($"Target export archive ({targetExportArchiveFullPath}) already exists.");
        }

        _fileSystemProvider.CreateDirectory(Path.GetDirectoryName(targetExportArchiveFullPath));
        return _fileSystemProvider.Open(targetExportArchiveFullPath, FileMode.CreateNew);
    }

    private void ConnectToHost()
    {
        var isConnected = IPAddress.TryParse(_host, out var ipAddress)
            ? _smbClient.Connect(ipAddress, SMBTransportType.DirectTCPTransport)
            : _smbClient.Connect(_host, SMBTransportType.DirectTCPTransport);

        if (!isConnected)
        {
            throw new OctoshiftCliException($"Unable to connect to host \"{_host}\".");
        }
    }

    private void Login()
    {
        var status = _smbClient.Login(_domainName ?? "", _smbUser, _smbPassword);

        if (!IsSuccessStatus(status))
        {
            throw new OctoshiftCliException($"Unable to login with provided credentials (Status Code: {status}).");
        }
    }

    private ISMBFileStore CreateSmbFileStore(string shareName)
    {
        var fileStore = _smbClient.TreeConnect(shareName, out var status);

        return IsSuccessStatus(status)
            ? fileStore
            : throw new OctoshiftCliException($"Unable to connect to share \"{shareName}\" (Status Code: {status}). " +
                                              "Please make sure that the directory is shared and the share name is correct.");
    }

    private object CreateFileHandle(ISMBFileStore fileStore, string sharedFilePath)
    {
        var status = fileStore.CreateFile(
            out var sharedFileHandle,
            out _,
            sharedFilePath,
            AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE,
            FileAttributes.Normal,
            ShareAccess.Read,
            CreateDisposition.FILE_OPEN,
            CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT,
            null);

        return IsSuccessStatus(status)
            ? sharedFileHandle
            : throw new OctoshiftCliException($"Couldn't create SMB file handle for \"{sharedFilePath}\" (Status Code: {status}).");
    }

    private long? GetFileSize(ISMBFileStore fileStore, object sharedFileHandle)
    {
        var status = fileStore.GetFileInformation(out var fileInfo, sharedFileHandle, FileInformationClass.FileStandardInformation);

        return !IsSuccessStatus(status) ? null : (fileInfo as FileStandardInformation)?.AllocationSize;
    }

    private bool IsSuccessStatus(NTStatus status) => status is NTStatus.STATUS_SUCCESS;

    private bool IsEndOfFileStatus(NTStatus status) => status is NTStatus.STATUS_END_OF_FILE;
}
