using System;
using OctoshiftCLI.BbsToGithub.Services;

namespace OctoshiftCLI.BbsToGithub;

public class BbsArchiveDownloaderFactory
{
    private readonly OctoLogger _log;
    private readonly FileSystemProvider _fileSystemProvider;

    public BbsArchiveDownloaderFactory(OctoLogger log, FileSystemProvider fileSystemProvider)
    {
        _log = log;
        _fileSystemProvider = fileSystemProvider;
    }

    public virtual IBbsArchiveDownloader CreateSshDownloader(string host, string sshUser, string privateKeyFileFullPath, int sshPort = 22, string bbsSharedHomeDirectory = null) =>
        new BbsSshArchiveDownloader(_log, _fileSystemProvider, host, sshUser, privateKeyFileFullPath, sshPort)
        {
            BbsSharedHomeDirectory = bbsSharedHomeDirectory ?? IBbsArchiveDownloader.DEFAULT_BBS_SHARED_HOME_DIRECTORY
        };

    public virtual IBbsArchiveDownloader CreateSmbDownloader() => throw new NotImplementedException();
}
