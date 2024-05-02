using System;
using System.IO;
using System.Threading.Tasks;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;
using Renci.SshNet;

namespace OctoshiftCLI.BbsToGithub.Services;

public sealed class BbsSshArchiveDownloader : IBbsArchiveDownloader, IDisposable
{
    private const int DOWNLOAD_PROGRESS_REPORT_INTERVAL_IN_SECONDS = 10;

    private readonly ISftpClient _sftpClient;
    private readonly PrivateKeyFile _privateKey;
    private readonly OctoLogger _log;
    private readonly FileSystemProvider _fileSystemProvider;
    private readonly object _mutex = new();
    private DateTime _nextProgressReport;

    public BbsSshArchiveDownloader(OctoLogger log, FileSystemProvider fileSystemProvider, string host, string sshUser, string privateKeyFileFullPath, int sshPort = 22)
    {
        _log = log;
        _fileSystemProvider = fileSystemProvider;

        _privateKey = new PrivateKeyFile(privateKeyFileFullPath);
        _sftpClient = new SftpClient(host, sshPort, sshUser, _privateKey);
    }

    internal BbsSshArchiveDownloader(OctoLogger log, FileSystemProvider fileSystemProvider, ISftpClient sftpClient)
    {
        _log = log;
        _fileSystemProvider = fileSystemProvider;
        _sftpClient = sftpClient;
    }

    public string BbsSharedHomeDirectory { get; init; } = BbsSettings.DEFAULT_BBS_SHARED_HOME_DIRECTORY_LINUX;

    private string GetSourceExportArchiveAbsolutePath(long exportJobId) =>
        IBbsArchiveDownloader.GetSourceExportArchiveAbsolutePath(BbsSharedHomeDirectory ?? BbsSettings.DEFAULT_BBS_SHARED_HOME_DIRECTORY_LINUX, exportJobId).ToUnixPath();

    public async Task<string> Download(long exportJobId, string targetDirectory = IBbsArchiveDownloader.DEFAULT_TARGET_DIRECTORY)
    {
        _nextProgressReport = DateTime.Now;

        var sourceExportArchiveFullPath = GetSourceExportArchiveAbsolutePath(exportJobId);
        var targetExportArchiveFullPath =
            Path.Join(targetDirectory ?? IBbsArchiveDownloader.DEFAULT_TARGET_DIRECTORY, IBbsArchiveDownloader.GetExportArchiveFileName(exportJobId)).ToUnixPath();

        if (_sftpClient is BaseClient { IsConnected: false } client)
        {
            client.Connect();
        }

        if (!_sftpClient.Exists(sourceExportArchiveFullPath))
        {
            throw new OctoshiftCliException(
                $"Source export archive ({sourceExportArchiveFullPath}) does not exist." +
                (BbsSharedHomeDirectory is BbsSettings.DEFAULT_BBS_SHARED_HOME_DIRECTORY_LINUX
                    ? "This most likely means that your Bitbucket instance uses a non-default Bitbucket shared home directory, so we couldn't find your archive. " +
                      "You can point the CLI to a non-default shared directory by specifying the --bbs-shared-home option."
                    : ""));
        }

        _fileSystemProvider.CreateDirectory(targetDirectory);

        var sourceExportArchiveSize = _sftpClient.GetAttributes(sourceExportArchiveFullPath)?.Size ?? long.MaxValue;
        await using var targetExportArchive = _fileSystemProvider.Open(targetExportArchiveFullPath, FileMode.Create);
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

            _log.LogInformation($"Archive download in progress, {GetLogFriendlySize(downloadedBytes)} out of {GetLogFriendlySize(totalBytes)} ({GetPercentage(downloadedBytes, totalBytes)}) completed...");

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

    public void Dispose()
    {
        (_sftpClient as IDisposable)?.Dispose();
        (_privateKey as IDisposable)?.Dispose();
    }
}
