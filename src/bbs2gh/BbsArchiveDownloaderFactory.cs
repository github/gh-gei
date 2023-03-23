using OctoshiftCLI.BbsToGithub.Services;

namespace OctoshiftCLI.BbsToGithub;

public class BbsArchiveDownloaderFactory
{
    private readonly OctoLogger _log;
    private readonly FileSystemProvider _fileSystemProvider;
    private readonly EnvironmentVariableProvider _environmentVariableProvider;

    public BbsArchiveDownloaderFactory(OctoLogger log, FileSystemProvider fileSystemProvider, EnvironmentVariableProvider environmentVariableProvider)
    {
        _log = log;
        _fileSystemProvider = fileSystemProvider;
        _environmentVariableProvider = environmentVariableProvider;
    }

    public virtual IBbsArchiveDownloader CreateSshDownloader(string host, string sshUser, string privateKeyFileFullPath, int sshPort = 22, string bbsSharedHomeDirectory = null) =>
        new BbsSshArchiveDownloader(_log, _fileSystemProvider, host, sshUser, privateKeyFileFullPath, sshPort)
        {
            BbsSharedHomeDirectory = bbsSharedHomeDirectory ?? BbsSettings.DEFAULT_BBS_SHARED_HOME_DIRECTORY_LINUX
        };

    public virtual IBbsArchiveDownloader CreateSmbDownloader(string host, string smbUser, string smbPassword, string domainName = null, string bbsSharedHomeDirectory = null) =>
        new BbsSmbArchiveDownloader(_log, _fileSystemProvider, host, smbUser, smbPassword ?? _environmentVariableProvider.SmbPassword(), domainName)
        {
            BbsSharedHomeDirectory = bbsSharedHomeDirectory ?? BbsSettings.DEFAULT_BBS_SHARED_HOME_DIRECTORY_WINDOWS
        };
}
