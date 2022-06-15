using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace OctoshiftCLI.AdoToGithub.Commands
{
    public class ShareServiceConnectionCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly AdoApiFactory _adoApiFactory;

        public ShareServiceConnectionCommand(OctoLogger log, AdoApiFactory adoApiFactory) : base("share-service-connection")
        {
            _log = log;
            _adoApiFactory = adoApiFactory;

            Description = "Makes an existing GitHub Pipelines App service connection available in another team project. This is required before you can rewire pipelines.";
            Description += Environment.NewLine;
            Description += "Note: Expects ADO_PAT env variable or --ado-pat option to be set.";

            var adoOrg = new Option<string>("--ado-org")
            {
                IsRequired = true
            };
            var adoTeamProject = new Option<string>("--ado-team-project")
            {
                IsRequired = true
            };
            var serviceConnectionId = new Option<string>("--service-connection-id")
            {
                IsRequired = true
            };
            var adoPat = new Option<string>("--ado-pat")
            {
                IsRequired = false
            };
            var verbose = new Option("--verbose")
            {
                IsRequired = false
            };

            AddOption(adoOrg);
            AddOption(adoTeamProject);
            AddOption(serviceConnectionId);
            AddOption(adoPat);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, string, string, bool>(Invoke);
        }

        public async Task Invoke(string adoOrg, string adoTeamProject, string serviceConnectionId, string adoPat = null, bool verbose = false)
        {
            _log.Verbose = verbose;

            _log.LogInformation("Sharing Service Connection...");
            _log.LogInformation($"ADO ORG: {adoOrg}");
            _log.LogInformation($"ADO TEAM PROJECT: {adoTeamProject}");
            _log.LogInformation($"SERVICE CONNECTION ID: {serviceConnectionId}");
            if (adoPat is not null)
            {
                _log.LogInformation("ADO PAT: ***");
            }

            var ado = _adoApiFactory.Create(adoPat);

            var adoTeamProjectId = await ado.GetTeamProjectId(adoOrg, adoTeamProject);

            if (await ado.ContainsServiceConnection(adoOrg, adoTeamProject, serviceConnectionId))
            {
                _log.LogInformation("Service connection already shared with team project");
                return;
            }

            await ado.ShareServiceConnection(adoOrg, adoTeamProject, adoTeamProjectId, serviceConnectionId);

            _log.LogSuccess("Successfully shared service connection");
        }
    }
}
