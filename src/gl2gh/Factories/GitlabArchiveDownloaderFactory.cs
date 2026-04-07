using OctoshiftCLI.GitlabToGithub.Services;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.GitlabToGithub.Factories;

public class GitlabArchiveDownloaderFactory
{
    private readonly OctoLogger _log;
    private readonly FileSystemProvider _fileSystemProvider;
    private readonly EnvironmentVariableProvider _environmentVariableProvider;

    public GitlabArchiveDownloaderFactory(OctoLogger log, FileSystemProvider fileSystemProvider, EnvironmentVariableProvider environmentVariableProvider)
    {
        _log = log;
        _fileSystemProvider = fileSystemProvider;
        _environmentVariableProvider = environmentVariableProvider;
    }

    public virtual IGitlabArchiveDownloader CreateSshDownloader(string host, string sshUser, string privateKeyFileFullPath, int sshPort = 22, string bbsSharedHomeDirectory = null) =>
        new GitlabSshArchiveDownloader(_log, _fileSystemProvider, host, sshUser, privateKeyFileFullPath, sshPort)
        {
            GitlabSharedHomeDirectory = bbsSharedHomeDirectory ?? GitlabSettings.DEFAULT_BBS_SHARED_HOME_DIRECTORY_LINUX
        };

    public virtual IGitlabArchiveDownloader CreateSmbDownloader(string host, string smbUser, string smbPassword, string domainName = null, string bbsSharedHomeDirectory = null) =>
        new GitlabSmbArchiveDownloader(_log, _fileSystemProvider, host, smbUser, smbPassword ?? _environmentVariableProvider.SmbPassword(), domainName)
        {
            GitlabSharedHomeDirectory = bbsSharedHomeDirectory ?? GitlabSettings.DEFAULT_BBS_SHARED_HOME_DIRECTORY_WINDOWS
        };
}
