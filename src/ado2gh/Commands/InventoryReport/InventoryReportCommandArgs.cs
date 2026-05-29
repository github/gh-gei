using OctoshiftCLI.Commands;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.AdoToGithub.Commands.InventoryReport
{
    public class InventoryReportCommandArgs : CommandArgs
    {
        public string AdoOrg { get; set; }
        public string AdoTeamProject { get; set; }
        [Secret]
        public string AdoPat { get; set; }
        public bool Minimal { get; set; }

        public override void Validate(OctoLogger log)
        {
            if (string.IsNullOrEmpty(AdoOrg) && !string.IsNullOrEmpty(AdoTeamProject))
            {
                throw new OctoshiftCliException("The --ado-team-project option requires the --ado-org option to also be provided.");
            }
        }
    }
}
