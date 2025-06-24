using System;
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
            if (GithubSourceOrg.HasValue() && Uri.IsWellFormedUriString(GithubSourceOrg, UriKind.Absolute))
            {
                throw new OctoshiftCliException("GithubSourceOrg should be an org name, not a URL.");
            }

            if (GithubTargetOrg.HasValue() && Uri.IsWellFormedUriString(GithubTargetOrg, UriKind.Absolute))
            {
                throw new OctoshiftCliException("GithubTargetOrg should be an org name, not a URL.");
            }
            if(GithubTargetEnterprise.HasValue() && Uri.IsWellFormedUriString(GithubTargetEnterprise, UriKind.Absolute))
            {
                throw new OctoshiftCliException("GithubTargetEnterprise should be an enterprise name, not a URL.");
            }
            if (GithubTargetPat.HasValue() && GithubSourcePat.IsNullOrWhiteSpace())
            {
                GithubSourcePat = GithubTargetPat;
                log?.LogInformation("Since github-target-pat is provided, github-source-pat will also use its value.");
            }
        }
    }
}
