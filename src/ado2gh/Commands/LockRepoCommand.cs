using System;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;

namespace OctoshiftCLI.AdoToGithub.Commands
{
    public class LockRepoCommand : Command
    {
        public LockRepoCommand(OctoLogger log, AdoApiFactory adoApiFactory) : base(
            name: "lock-ado-repo",
            description: "Makes the ADO repo read-only for all users. It does this by adding Deny permissions for the Project Valid Users group on the repo." +
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
            var adoRepo = new Option<string>("--ado-repo")
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
            AddOption(adoRepo);
            AddOption(adoPat);
            AddOption(verbose);

            var handler = new LockRepoCommandHandler(log, adoApiFactory);
            Handler = CommandHandler.Create<LockRepoCommandArgs>(handler.Invoke);
        }
    }

    public class LockRepoCommandArgs
    {
        public string AdoOrg { get; set; }
        public string AdoTeamProject { get; set; }
        public string AdoRepo { get; set; }
        public string AdoPat { get; set; }
        public bool Verbose { get; set; }
    }
}
