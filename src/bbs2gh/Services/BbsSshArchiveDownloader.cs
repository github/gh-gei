using System;
using System.IO;
using System.Threading.Tasks;
using Renci.SshNet;

namespace OctoshiftCLI.BbsToGithub.Services;

public sealed class BbsSshArchiveDownloader : IBbsArchiveDownloader, IDisposable
{
    private const int DOWNLOAD_PROGRESS_REPORT_INTERVAL_IN_SECONDS = 10;

    private readonly ISftpClient _sftpClient;
    private readonly OctoLogger _log;
    private readonly FileSystemProvider _fileSystemProvider;
    private readonly object _mutex = new();
    private DateTime _nextProgressReport;

#pragma warning disable CA2000 // Incorrectly flagged as a not-disposing error
    public BbsSshArchiveDownloader(OctoLogger log, FileSystemProvider fileSystemProvider, string host, string sshUser, string privateKeyFileFullPath, int sshPort = 22)
    {
        var pkRsa = new PrivateKeyFile(privateKeyFileFullPath);
        var newKey = RsaSha256Util.ConvertToKeyWithSha256Signature(pkRsa);
        RsaSha256Util.UpdatePrivateKeyFile(pkRsa, newKey);
        var authenticationMethodRsa = new PrivateKeyAuthenticationMethod(sshUser, pkRsa);
        var conn = new ConnectionInfo(host, sshPort, sshUser, authenticationMethodRsa);
        RsaSha256Util.SetupConnection(conn);

        _sftpClient = new SftpClient(conn);

        _log = log;
        _fileSystemProvider = fileSystemProvider;
    }
#pragma warning restore CA2000

    internal BbsSshArchiveDownloader(OctoLogger log, FileSystemProvider fileSystemProvider, ISftpClient sftpClient)
    {
        _log = log;
        _fileSystemProvider = fileSystemProvider;
        _sftpClient = sftpClient;
    }

    public string BbsSharedHomeDirectory { get; init; } = IBbsArchiveDownloader.DEFAULT_BBS_SHARED_HOME_DIRECTORY;

    public async Task<string> Download(long exportJobId, string targetDirectory = IBbsArchiveDownloader.DEFAULT_TARGET_DIRECTORY)
    {
        _nextProgressReport = DateTime.Now;

        var sourceExportArchiveFullPath = Path.Join(BbsSharedHomeDirectory ?? IBbsArchiveDownloader.DEFAULT_BBS_SHARED_HOME_DIRECTORY,
            IBbsArchiveDownloader.GetSourceExportArchiveRelativePath(exportJobId)).Replace('\\', '/');
        var targetExportArchiveFullPath =
            Path.Join(targetDirectory ?? IBbsArchiveDownloader.DEFAULT_TARGET_DIRECTORY, IBbsArchiveDownloader.GetExportArchiveFileName(exportJobId)).Replace('\\', '/');

        if (_fileSystemProvider.FileExists(targetExportArchiveFullPath))
        {
            throw new OctoshiftCliException($"Target export archive ({targetExportArchiveFullPath}) already exists.");
        }

        if (_sftpClient is BaseClient { IsConnected: false } client)
        {
            client.Connect();
        }

        if (!_sftpClient.Exists(sourceExportArchiveFullPath))
        {
            throw new OctoshiftCliException($"Source export archive ({sourceExportArchiveFullPath}) does not exist.");
        }

        _fileSystemProvider.CreateDirectory(targetDirectory);

        var sourceExportArchiveSize = _sftpClient.GetAttributes(sourceExportArchiveFullPath)?.Size ?? long.MaxValue;
        await using var targetExportArchive = _fileSystemProvider.Open(targetExportArchiveFullPath, FileMode.CreateNew);
        await Task.Factory.FromAsync(
            _sftpClient.BeginDownloadFile(
                sourceExportArchiveFullPath,
                targetExportArchive,
                null,
                null,
                downloaded => LogProgress(downloaded, (ulong)sourceExportArchiveSize)),
            _sftpClient.EndDownloadFile);

        return targetExportArchiveFullPath;
    }

    private void LogProgress(ulong downloadedBytes, ulong totalBytes)
    {
        lock (_mutex)
        {
            if (DateTime.Now < _nextProgressReport)
            {
                return;
            }

            _log.LogInformation($"Download archive in progress, {GetLogFriendlySize(downloadedBytes)} out of {GetLogFriendlySize(totalBytes)} ({GetPercentage(downloadedBytes, totalBytes)}) completed...");

            _nextProgressReport = _nextProgressReport.AddSeconds(DOWNLOAD_PROGRESS_REPORT_INTERVAL_IN_SECONDS);
        }
    }

    private string GetPercentage(ulong downloadedBytes, ulong totalBytes)
    {
        if (totalBytes is ulong.MinValue)
        {
            return "unknown%";
        }

        var percentage = (int)(downloadedBytes * 100D / totalBytes);
        return $"{percentage}%";
    }

    private string GetLogFriendlySize(ulong size)
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

    public void Dispose() => (_sftpClient as IDisposable)?.Dispose();
}
