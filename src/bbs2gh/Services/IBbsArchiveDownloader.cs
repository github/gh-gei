using System.IO;
using System.Threading.Tasks;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.BbsToGithub.Services;

public interface IBbsArchiveDownloader
{
    const string EXPORT_ARCHIVE_SOURCE_DIRECTORY = "data/migration/export";
    const string DEFAULT_TARGET_DIRECTORY = "bbs_archive_downloads";

    Task<string> Download(long exportJobId, string repoName, string targetDirectory = DEFAULT_TARGET_DIRECTORY);

    string GetSourceExportArchiveAbsolutePath(long exportJobId, string repoName);

    static string GetExportArchiveFileName(long exportJobId, string repoName) => $"Bitbucket_export_{repoName}_{exportJobId}.tar";

    static string GetSourceExportArchiveRelativePath(long exportJobId, string repoName) => Path.Join(EXPORT_ARCHIVE_SOURCE_DIRECTORY, GetExportArchiveFileName(exportJobId, repoName)).ToUnixPath();
}
