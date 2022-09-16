using System.Threading.Tasks;

namespace OctoshiftCLI.BbsToGithub.Services;

public interface IBbsArchiveDownloader
{
    const string DEFAULT_TARGET_DIRECTORY = "bbs_archive_downloads";

    Task<string> Download(long exportJobId, string targetDirectory = DEFAULT_TARGET_DIRECTORY);
}
