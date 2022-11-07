using System.IO;
using System.Threading.Tasks;

namespace OctoshiftCLI.BbsToGithub.Services;

public interface IBbsArchiveDownloader
{
    const string DEFAULT_BBS_SHARED_HOME_DIRECTORY = "/var/atlassian/application-data/bitbucket/shared";
    const string EXPORT_ARCHIVE_SOURCE_DIRECTORY = "data/migration/export";
    const string DEFAULT_TARGET_DIRECTORY = "bbs_archive_downloads";

    Task<string> Download(long exportJobId, string targetDirectory = DEFAULT_TARGET_DIRECTORY);

    string GetSourceExportArchiveAbsolutePath(long exportJobId);

    static string GetExportArchiveFileName(long exportJobId) => $"Bitbucket_export_{exportJobId}.tar";

    static string GetSourceExportArchiveRelativePath(long exportJobId) => Path.Join(EXPORT_ARCHIVE_SOURCE_DIRECTORY, GetExportArchiveFileName(exportJobId)).Replace('\\', '/');
}
