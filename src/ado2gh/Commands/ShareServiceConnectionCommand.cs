using System;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using OctoshiftCLI.AdoToGithub.Handlers;

namespace OctoshiftCLI.AdoToGithub.Commands
{
    public class ShareServiceConnectionCommand : Command
    {
        public ShareServiceConnectionCommand(OctoLogger log, AdoApiFactory adoApiFactory) : base(
            name: "share-service-connection",
            description: "Makes an existing GitHub Pipelines App service connection available in another team project. This is required before you can rewire pipelines." +
                         Environment.NewLine +
                         "Note: Expects ADO_PAT env variable or --ado-pat option to be set.")
        {
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
            var verbose = new Option<bool>("--verbose")
            {
                IsRequired = false
            };

            AddOption(adoOrg);
            AddOption(adoTeamProject);
            AddOption(serviceConnectionId);
            AddOption(adoPat);
            AddOption(verbose);

            var handler = new ShareServiceConnectionCommandHandler(log, adoApiFactory);
            Handler = CommandHandler.Create<ShareServiceConnectionCommandArgs>(handler.Invoke);
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
