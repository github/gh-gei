using System.IO;
using OctoshiftCLI.Commands;

namespace OctoshiftCLI.AdoToGithub.Commands.GenerateScript
{
    public class GenerateScriptCommandArgs : CommandArgs
    {
        public string GithubOrg { get; set; }
        public string AdoOrg { get; set; }
        public string AdoTeamProject { get; set; }
        public string AdoServerUrl { get; set; }
        public FileInfo Output { get; set; }
        public bool Sequential { get; set; }
        [Secret]
        public string AdoPat { get; set; }
        public bool DownloadMigrationLogs { get; set; }
        public bool CreateTeams { get; set; }
        public bool LinkIdpGroups { get; set; }
        public bool LockAdoRepos { get; set; }
        public bool DisableAdoRepos { get; set; }
        public bool RewirePipelines { get; set; }
        public bool All { get; set; }
        public FileInfo RepoList { get; set; }
        public string TargetApiUrl { get; set; }
    }
}
