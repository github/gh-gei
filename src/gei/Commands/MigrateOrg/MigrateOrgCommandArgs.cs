using OctoshiftCLI.Commands;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands.MigrateOrg
{
    public class MigrateOrgCommandArgs : CommandArgs
    {
        public string GithubSourceOrg { get; set; }
        public string GithubTargetOrg { get; set; }
        public string GithubTargetEnterprise { get; set; }
        public bool QueueOnly { get; set; }
        [Secret]
        public string GithubSourcePat { get; set; }
        [Secret]
        public string GithubTargetPat { get; set; }
        public string TargetApiUrl { get; set; }
        public string TargetUploadsUrl { get; set; }

        public override void Validate(OctoLogger log)
        {
            if (GithubSourceOrg.IsUrl())
            {
                throw new OctoshiftCliException($"The --github-source-org option expects an organization name, not a URL. Please provide just the organization name (e.g., 'my-org' instead of 'https://github.com/my-org').");
            }

            if (GithubTargetOrg.IsUrl())
            {
                throw new OctoshiftCliException($"The --github-target-org option expects an organization name, not a URL. Please provide just the organization name (e.g., 'my-org' instead of 'https://github.com/my-org').");
            }

            if (GithubTargetEnterprise.IsUrl())
            {
                throw new OctoshiftCliException($"The --github-target-enterprise option expects an enterprise name, not a URL. Please provide just the enterprise name (e.g., 'my-enterprise' instead of 'https://github.com/enterprises/my-enterprise').");
            }

            if (GithubTargetPat.HasValue() && GithubSourcePat.IsNullOrWhiteSpace())
            {
                GithubSourcePat = GithubTargetPat;
                log?.LogInformation("Since github-target-pat is provided, github-source-pat will also use its value.");
            }
        }
    }
}
