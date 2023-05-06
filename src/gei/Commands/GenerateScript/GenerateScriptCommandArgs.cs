using System.IO;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands.GenerateScript
{
    public class GenerateScriptCommandArgs
    {
        public string GithubSourceOrg { get; set; }
        public string AdoServerUrl { get; set; }
        public string AdoSourceOrg { get; set; }
        public string AdoTeamProject { get; set; }
        public string GithubTargetOrg { get; set; }
        public FileInfo Output { get; set; }
        public string GhesApiUrl { get; set; }
        public string AwsBucketName { get; set; }
        public string AwsRegion { get; set; }
        public bool NoSslVerify { get; set; }
        public bool SkipReleases { get; set; }
        public bool LockSourceRepo { get; set; }
        public bool DownloadMigrationLogs { get; set; }
        public bool Sequential { get; set; }
        public string GithubSourcePat { get; set; }
        public string AdoPat { get; set; }
        public bool Verbose { get; set; }
        public bool KeepArchive { get; set; }
    }
}
