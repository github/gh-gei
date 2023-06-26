using OctoshiftCLI.Commands;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands.InventoryReport
{
    public class InventoryReportCommandArgs : CommandArgs
    {
        public string GithubOrg { get; set; }
        [Secret]
        public string GithubPat { get; set; }
        public string GhesApiUrl { get; set; }
        public bool NoSslVerify { get; set; }
        public bool Minimal { get; set; }

        public override void Validate(OctoLogger log)
        {
            if (NoSslVerify && GhesApiUrl.IsNullOrWhiteSpace())
            {
                throw new OctoshiftCliException("NoSslVerify can only be used when targeting GHES. SSL verification is always enabled for GitHub.com.");
            }
        }
    }
}
