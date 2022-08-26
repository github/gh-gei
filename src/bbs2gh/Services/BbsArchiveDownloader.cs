using System;
using System.IO;
using System.Threading.Tasks;
using Renci.SshNet;

namespace OctoshiftCLI.BbsToGithub.Services;

public class BbsArchiveDownloader : IDisposable
{
    private const int DOWNLOAD_PROGRESS_REPORT_INTERVAL_IN_SECONDS = 10;
    private const string DEFAULT_BBS_SHARED_HOME_DIRECTORY = "/var/atlassian/application-data/bitbucket/shared";
    private const string EXPORT_ARCHIVE_SOURCE_DIRECTORY = "data/migration/export";

    private readonly ISftpClient _sftpClient;
    private readonly OctoLogger _log;
    private DateTime _nextProgressReport;

    internal FileSystemProvider FileSystemProvider = new();

    public BbsArchiveDownloader(OctoLogger log, string host, string sshUser, string privateKeyFileFullPath, int sshPort = 22)
    {
        _log = log;
        _sftpClient = new SftpClient(host, sshPort, sshUser, new PrivateKeyFile(privateKeyFileFullPath));
    }

    internal BbsArchiveDownloader(OctoLogger log, ISftpClient sftpClient)
    {
        _log = log;
        _sftpClient = sftpClient;
    }

    public virtual string BbsSharedHomeDirectory { get; init; } = DEFAULT_BBS_SHARED_HOME_DIRECTORY;

    public virtual async Task Download(long exportJobId, string targetDirectory = "bbs_archive_downloads")
    {
        _nextProgressReport = DateTime.Now;

        var exportArchiveFilename = $"Bitbucket_export_{exportJobId}.tar";
        var sourceExportArchiveFullPath = Path.Join(BbsSharedHomeDirectory, EXPORT_ARCHIVE_SOURCE_DIRECTORY, exportArchiveFilename);
        var targetExportArchiveFullPath = Path.Join(targetDirectory, exportArchiveFilename);

        if (FileSystemProvider.FileExists(targetExportArchiveFullPath))
        {
            throw new OctoshiftCliException($"Target export archive ({targetExportArchiveFullPath}) already exists.");
        }

        if (_sftpClient is BaseClient { IsConnected: false } clinet)
        {
            clinet.Connect();
        }

        if (!_sftpClient.Exists(sourceExportArchiveFullPath))
        {
            throw new OctoshiftCliException($"Source export archive ({sourceExportArchiveFullPath}) does not exist.");
        }

        FileSystemProvider.CreateDirectory(targetDirectory);

        var sourceExportArchiveSize = _sftpClient.GetAttributes(sourceExportArchiveFullPath)?.Size ?? long.MaxValue;
        await using var targetExportArchive = FileSystemProvider.Open(targetExportArchiveFullPath, FileMode.CreateNew);
        await Task.Factory.FromAsync(
            _sftpClient.BeginDownloadFile(
                sourceExportArchiveFullPath,
                targetExportArchive,
                null,
                null,
                downloaded => LogProgress(downloaded, (ulong)sourceExportArchiveSize)),
            _sftpClient.EndDownloadFile);
    }

    private void LogProgress(ulong downloadedBytes, ulong totalBytes)
    {
        if (DateTime.Now < _nextProgressReport)
        {
            return;
        }

        var percentComplete = (int)(downloadedBytes * 100M / totalBytes);
        _log.LogInformation($"Downloading archive in progress ({percentComplete}% completed)...");

        _nextProgressReport = _nextProgressReport.AddSeconds(DOWNLOAD_PROGRESS_REPORT_INTERVAL_IN_SECONDS);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            (_sftpClient as IDisposable)?.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
