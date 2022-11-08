using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Renci.SshNet;
using Renci.SshNet.Security;

namespace OctoshiftCLI.BbsToGithub.Services;

public sealed class BbsSshArchiveDownloader : IBbsArchiveDownloader, IDisposable
{
    private const int DOWNLOAD_PROGRESS_REPORT_INTERVAL_IN_SECONDS = 10;

    private readonly ISftpClient _sftpClient;
    private readonly RsaKey _rsaKey;
    private readonly PrivateKeyFile _privateKey;
    private readonly PrivateKeyAuthenticationMethod _authenticationMethodRsa;
    private readonly OctoLogger _log;
    private readonly FileSystemProvider _fileSystemProvider;
    private readonly object _mutex = new();
    private DateTime _nextProgressReport;

    public BbsSshArchiveDownloader(OctoLogger log, FileSystemProvider fileSystemProvider, string host, string sshUser, string privateKeyFileFullPath, int sshPort = 22)
    {
        _log = log;
        _fileSystemProvider = fileSystemProvider;

        _privateKey = new PrivateKeyFile(privateKeyFileFullPath);

        if (IsRsaKey(_privateKey))
        {
            _rsaKey = UpdatePrivateKeyFileToRsaSha256(_privateKey);
            _authenticationMethodRsa = new PrivateKeyAuthenticationMethod(sshUser, _privateKey);
            var connection = new ConnectionInfo(host, sshPort, sshUser, _authenticationMethodRsa);
            connection.HostKeyAlgorithms["rsa-sha2-256"] = data => new KeyHostAlgorithm("rsa-sha2-256", _rsaKey, data);
            _sftpClient = new SftpClient(connection);
        }
        else
        {
            _sftpClient = new SftpClient(host, sshPort, sshUser, _privateKey);
        }
    }

    private bool IsRsaKey(PrivateKeyFile privateKeyFile) => privateKeyFile.HostKey is KeyHostAlgorithm keyHostAlgorithm && keyHostAlgorithm.Key is RsaKey;

    private RsaWithSha256SignatureKey UpdatePrivateKeyFileToRsaSha256(PrivateKeyFile privateKeyFile)
    {
        if ((privateKeyFile.HostKey as KeyHostAlgorithm).Key is not RsaKey oldRsaKey)
        {
            throw new ArgumentException("The private key file is not an RSA key.", nameof(privateKeyFile));
        }

        var rsaKey = new RsaWithSha256SignatureKey(oldRsaKey.Modulus, oldRsaKey.Exponent, oldRsaKey.D, oldRsaKey.P, oldRsaKey.Q, oldRsaKey.InverseQ);

        var keyHostAlgorithm = new KeyHostAlgorithm(rsaKey.ToString(), rsaKey);

        var hostKeyProperty = typeof(PrivateKeyFile).GetProperty(nameof(PrivateKeyFile.HostKey));
        hostKeyProperty.SetValue(privateKeyFile, keyHostAlgorithm);

        var keyField = typeof(PrivateKeyFile).GetField("_key", BindingFlags.NonPublic | BindingFlags.Instance);
        keyField.SetValue(privateKeyFile, rsaKey);

        return rsaKey;
    }

    internal BbsSshArchiveDownloader(OctoLogger log, FileSystemProvider fileSystemProvider, ISftpClient sftpClient)
    {
        _log = log;
        _fileSystemProvider = fileSystemProvider;
        _sftpClient = sftpClient;
    }

    public string BbsSharedHomeDirectory { get; init; } = IBbsArchiveDownloader.DEFAULT_BBS_SHARED_HOME_DIRECTORY;

    public string GetSourceExportArchiveAbsolutePath(long exportJobId)
    {
        return Path.Join(BbsSharedHomeDirectory ?? IBbsArchiveDownloader.DEFAULT_BBS_SHARED_HOME_DIRECTORY, IBbsArchiveDownloader.GetSourceExportArchiveRelativePath(exportJobId)).Replace('\\', '/');
    }

    public async Task<string> Download(long exportJobId, string targetDirectory = IBbsArchiveDownloader.DEFAULT_TARGET_DIRECTORY)
    {
        _nextProgressReport = DateTime.Now;

        var sourceExportArchiveFullPath = GetSourceExportArchiveAbsolutePath(exportJobId);
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

    public void Dispose()
    {
        (_sftpClient as IDisposable)?.Dispose();
        (_rsaKey as IDisposable)?.Dispose();
        (_authenticationMethodRsa as IDisposable)?.Dispose();
        (_privateKey as IDisposable)?.Dispose();
    }
}
