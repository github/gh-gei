using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace OctoshiftCLI.AdoToGithub.Commands
{
    public class LockRepoCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly AdoApiFactory _adoApiFactory;

        public LockRepoCommand(OctoLogger log, AdoApiFactory adoApiFactory) : base("lock-ado-repo")
        {
            _log = log;
            _adoApiFactory = adoApiFactory;

            Description = "Makes the ADO repo read-only for all users. It does this by adding Deny permissions for the Project Valid Users group on the repo.";
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
            var adoRepo = new Option<string>("--ado-repo")
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
            AddOption(adoRepo);
            AddOption(adoPat);
            AddOption(verbose);

            Handler = CommandHandler.Create<LockRepoCommandArgs>(Invoke);
        }

        public async Task Invoke(LockRepoCommandArgs args)
        {
            if (args is null)
            {
                throw new ArgumentNullException(nameof(args));
            }
            
            _log.Verbose = args.Verbose;

            _log.LogInformation("Locking repo...");
            _log.LogInformation($"ADO ORG: {args.AdoOrg}");
            _log.LogInformation($"ADO TEAM PROJECT: {args.AdoTeamProject}");
            _log.LogInformation($"ADO REPO: {args.AdoRepo}");
            if (args.AdoPat is not null)
            {
                _log.LogInformation("ADO PAT: ***");
            }

            var ado = _adoApiFactory.Create(args.AdoPat);

            var teamProjectId = await ado.GetTeamProjectId(args.AdoOrg, args.AdoTeamProject);
            var repoId = await ado.GetRepoId(args.AdoOrg, args.AdoTeamProject, args.AdoRepo);

            var identityDescriptor = await ado.GetIdentityDescriptor(args.AdoOrg, teamProjectId, "Project Valid Users");
            await ado.LockRepo(args.AdoOrg, teamProjectId, repoId, identityDescriptor);

            _log.LogSuccess("Repo successfully locked");
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
