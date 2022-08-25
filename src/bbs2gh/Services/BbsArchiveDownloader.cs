using System;
using System.IO;
using OctoshiftCLI.Contracts;
using Renci.SshNet;

namespace OctoshiftCLI.BbsToGithub.Services;

public class BbsArchiveDownloader : IDisposable
{
    private const int DOWNLOAD_PROGRESS_REPORT_INTERVAL_IN_SECONDS = 10;
    private const string DEFAULT_BBS_SHARED_HOME_DIRECTORY = "/var/atlassian/application-data/bitbucket/shared";
    private const string EXPORT_ARCHIVE_SOURCE_DIRECTORY = "data/migration/export";

    private readonly ScpClient _scpClient;
    private readonly OctoLogger _log;
    private DateTime _nextProgressReport;

    internal IFileSystemProvider FileSystemProvider = new FileSystemProvider();
    internal Action<string, FileInfo> ScpDownload;

    public BbsArchiveDownloader(OctoLogger log, string host, string sshUser, string privateKeyFileFullPath, int sshPort = 22)
        : this(log, new ScpClient(host, sshPort, sshUser, new PrivateKeyFile(privateKeyFileFullPath)))
    {
    }

    internal BbsArchiveDownloader(OctoLogger log, ScpClient scpClient)
    {
        _log = log;
        _scpClient = scpClient;
        _scpClient.Downloading += (_, args) => LogProgress(args.Downloaded, args.Size);

        ScpDownload = DownloadFile;
    }

    public virtual string BbsSharedHomeDirectory { get; init; } = DEFAULT_BBS_SHARED_HOME_DIRECTORY;

    public virtual void Download(long exportJobId, string targetDirectory = "bbs_archive_downloads")
    {
        _nextProgressReport = DateTime.Now;

        var exportArchiveFilename = $"Bitbucket_export_{exportJobId}.tar";
        var sourceExportArchiveFullPath = Path.Join(BbsSharedHomeDirectory, EXPORT_ARCHIVE_SOURCE_DIRECTORY, exportArchiveFilename);
        var targetExportArchiveFullPath = Path.Join(targetDirectory, exportArchiveFilename);

        if (FileSystemProvider.FileExists(targetExportArchiveFullPath))
        {
            throw new OctoshiftCliException($"Target export archive ({targetExportArchiveFullPath}) already exists.");
        }

        FileSystemProvider.CreateDirectory(targetDirectory);

        ScpDownload(sourceExportArchiveFullPath, new FileInfo(targetExportArchiveFullPath));
    }

    private void DownloadFile(string sourceExportArchiveFullPath, FileInfo targetExportArchive)
    {
        if (!_scpClient.IsConnected)
        {
            _scpClient.Connect();
        }

        _scpClient.Download(sourceExportArchiveFullPath, targetExportArchive);
    }

    private void LogProgress(long downloadedBytes, long totalBytes)
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
            _scpClient?.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
