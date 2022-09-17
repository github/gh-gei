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

            Handler = CommandHandler.Create<ShareServiceConnectionCommandArgs>(Invoke);
        }

        public async Task Invoke(ShareServiceConnectionCommandArgs args)
        {
            if (args is null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            _log.Verbose = args.Verbose;

            _log.LogInformation("Sharing Service Connection...");
            _log.LogInformation($"ADO ORG: {args.AdoOrg}");
            _log.LogInformation($"ADO TEAM PROJECT: {args.AdoTeamProject}");
            _log.LogInformation($"SERVICE CONNECTION ID: {args.ServiceConnectionId}");
            if (args.AdoPat is not null)
            {
                _log.LogInformation("ADO PAT: ***");
            }

            var ado = _adoApiFactory.Create(args.AdoPat);

            var adoTeamProjectId = await ado.GetTeamProjectId(args.AdoOrg, args.AdoTeamProject);

            if (await ado.ContainsServiceConnection(args.AdoOrg, args.AdoTeamProject, args.ServiceConnectionId))
            {
                _log.LogInformation("Service connection already shared with team project");
                return;
            }

            await ado.ShareServiceConnection(args.AdoOrg, args.AdoTeamProject, adoTeamProjectId, args.ServiceConnectionId);

            _log.LogSuccess("Successfully shared service connection");
        }
    }

    public class ShareServiceConnectionCommandArgs
    {
        public string AdoOrg { get; set; }
        public string AdoTeamProject { get; set; }
        public string ServiceConnectionId { get; set; }
        public string AdoPat { get; set; }
        public bool Verbose { get; set; }
    }
}
