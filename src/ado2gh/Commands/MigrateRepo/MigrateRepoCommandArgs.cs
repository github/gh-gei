using OctoshiftCLI.Commands;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.AdoToGithub.Commands.MigrateRepo
{
    public class MigrateRepoCommandArgs : CommandArgs
    {
        public string AdoOrg { get; set; }
        public string AdoTeamProject { get; set; }
        public string AdoRepo { get; set; }
        public string GithubOrg { get; set; }
        public string GithubRepo { get; set; }
        public string AdoServerUrl { get; set; }
        public bool QueueOnly { get; set; }
        public string TargetRepoVisibility { get; set; }
        [Secret]
        public string AdoPat { get; set; }
        [Secret]
        public string GithubPat { get; set; }
        public string TargetApiUrl { get; set; }

        public override void Validate(OctoLogger log)
        {
            if (GithubOrg.IsUrl())
            {
                throw new OctoshiftCliException($"The --github-org option expects an organization name, not a URL. Please provide just the organization name (e.g., 'my-org' instead of 'https://github.com/my-org').");
            }

            if (GithubRepo.IsUrl())
            {
                throw new OctoshiftCliException($"The --github-repo option expects a repository name, not a URL. Please provide just the repository name (e.g., 'my-repo' instead of 'https://github.com/my-org/my-repo').");
            }
        }
    }
}
