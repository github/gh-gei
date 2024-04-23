using System;
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.AdoToGithub.Factories;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.AdoToGithub.Commands.LockRepo
{
    public class LockRepoCommand : CommandBase<LockRepoCommandArgs, LockRepoCommandHandler>
    {
        public LockRepoCommand() : base(
            name: "lock-ado-repo",
            description: "Makes the ADO repo read-only for all users. It does this by adding Deny permissions for the Project Valid Users group on the repo." +
                         Environment.NewLine +
                         "Note: Expects ADO_PAT env variable or --ado-pat option to be set.")
        {
            AddOption(AdoOrg);
            AddOption(AdoTeamProject);
            AddOption(AdoRepo);
            AddOption(AdoPat);
            AddOption(Verbose);
        }

        public Option<string> AdoOrg { get; } = new("--ado-org")
        {
            IsRequired = true
        };
        public Option<string> AdoTeamProject { get; } = new("--ado-team-project")
        {
            IsRequired = true
        };
        public Option<string> AdoRepo { get; } = new("--ado-repo")
        {
            IsRequired = true
        };
        public Option<string> AdoPat { get; } = new("--ado-pat")
        {
            Description = "An Azure DevOps personal access token with the 'Identity -> Read' and 'Security -> Manage' scopes."
        };
        public Option<bool> Verbose { get; } = new("--verbose");

        public override LockRepoCommandHandler BuildHandler(LockRepoCommandArgs args, IServiceProvider sp)
        {
            if (args is null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            if (sp is null)
            {
                throw new ArgumentNullException(nameof(sp));
            }

            var log = sp.GetRequiredService<OctoLogger>();
            var adoApiFactory = sp.GetRequiredService<AdoApiFactory>();
            var adoApi = adoApiFactory.Create(args.AdoPat);

            return new LockRepoCommandHandler(log, adoApi);
        }
    }
}
