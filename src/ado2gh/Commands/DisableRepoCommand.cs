using System;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.Linq;
using System.Threading.Tasks;

namespace OctoshiftCLI.AdoToGithub.Commands
{
    public class DisableRepoCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly AdoApiFactory _adoApiFactory;

        public DisableRepoCommand(OctoLogger log, AdoApiFactory adoApiFactory) : base(
            name: "disable-ado-repo",
            description: "Disables the repo in Azure DevOps. This makes the repo non-readable for all." +
                         Environment.NewLine +
                         "Note: Expects ADO_PAT env variable or --ado-pat option to be set.")
        {
            _log = log;
            _adoApiFactory = adoApiFactory;

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

            Handler = CommandHandler.Create<DisableRepoCommandArgs>(Invoke);
        }

        public async Task Invoke(DisableRepoCommandArgs args)
        {
            if (args is null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            _log.Verbose = args.Verbose;

            _log.LogInformation("Disabling repo...");
            _log.LogInformation($"ADO ORG: {args.AdoOrg}");
            _log.LogInformation($"ADO TEAM PROJECT: {args.AdoTeamProject}");
            _log.LogInformation($"ADO REPO: {args.AdoRepo}");
            if (args.AdoPat is not null)
            {
                _log.LogInformation("ADO PAT: ***");
            }

            var ado = _adoApiFactory.Create(args.AdoPat);

            var allRepos = await ado.GetRepos(args.AdoOrg, args.AdoTeamProject);
            if (allRepos.Any(r => r.Name == args.AdoRepo && r.IsDisabled))
            {
                _log.LogSuccess($"Repo '{args.AdoOrg}/{args.AdoTeamProject}/{args.AdoRepo}' is already disabled - No action will be performed");
                return;
            }
            var repoId = allRepos.First(r => r.Name == args.AdoRepo).Id;
            await ado.DisableRepo(args.AdoOrg, args.AdoTeamProject, repoId);

            _log.LogSuccess("Repo successfully disabled");
        }
    }

    public class DisableRepoCommandArgs
    {
        public string AdoOrg { get; set; }
        public string AdoTeamProject { get; set; }
        public string AdoRepo { get; set; }
        public string AdoPat { get; set; }
        public bool Verbose { get; set; }
    }
}
