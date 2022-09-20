using System;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.Threading.Tasks;

namespace OctoshiftCLI.AdoToGithub.Commands
{
    public class RewirePipelineCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly AdoApiFactory _adoApiFactory;

        public RewirePipelineCommand(OctoLogger log, AdoApiFactory adoApiFactory) : base(
            name: "rewire-pipeline",
            description: "Updates an Azure Pipeline to point to a GitHub repo instead of an Azure Repo." +
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
            var adoPipeline = new Option<string>("--ado-pipeline")
            {
                IsRequired = true,
                Description = "The path and/or name of your pipeline. If the pipeline is in the root pipeline folder this can be just the name. Otherwise you need to specify the full pipeline path (E.g. \\Services\\Finance\\CI-Pipeline)"
            };
            var githubOrg = new Option<string>("--github-org")
            {
                IsRequired = true
            };
            var githubRepo = new Option<string>("--github-repo")
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
            AddOption(adoPipeline);
            AddOption(githubOrg);
            AddOption(githubRepo);
            AddOption(serviceConnectionId);
            AddOption(adoPat);
            AddOption(verbose);

            Handler = CommandHandler.Create<RewirePipelineCommandArgs>(Invoke);
        }

        public async Task Invoke(RewirePipelineCommandArgs args)
        {
            if (args is null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            _log.Verbose = args.Verbose;

            _log.LogInformation($"Rewiring Pipeline to GitHub repo...");
            _log.LogInformation($"ADO ORG: {args.AdoOrg}");
            _log.LogInformation($"ADO TEAM PROJECT: {args.AdoTeamProject}");
            _log.LogInformation($"ADO PIPELINE: {args.AdoPipeline}");
            _log.LogInformation($"GITHUB ORG: {args.GithubOrg}");
            _log.LogInformation($"GITHUB REPO: {args.GithubRepo}");
            _log.LogInformation($"SERVICE CONNECTION ID: {args.ServiceConnectionId}");
            if (args.AdoPat is not null)
            {
                _log.LogInformation("ADO PAT: ***");
            }

            var ado = _adoApiFactory.Create(args.AdoPat);

            var adoPipelineId = await ado.GetPipelineId(args.AdoOrg, args.AdoTeamProject, args.AdoPipeline);
            var (defaultBranch, clean, checkoutSubmodules) = await ado.GetPipeline(args.AdoOrg, args.AdoTeamProject, adoPipelineId);
            await ado.ChangePipelineRepo(args.AdoOrg, args.AdoTeamProject, adoPipelineId, defaultBranch, clean, checkoutSubmodules, args.GithubOrg, args.GithubRepo, args.ServiceConnectionId);

            _log.LogSuccess("Successfully rewired pipeline");
        }
    }

    public class RewirePipelineCommandArgs
    {
        public string AdoOrg { get; set; }
        public string AdoTeamProject { get; set; }
        public string AdoPipeline { get; set; }
        public string GithubOrg { get; set; }
        public string GithubRepo { get; set; }
        public string ServiceConnectionId { get; set; }
        public string AdoPat { get; set; }
        public bool Verbose { get; set; }
    }
}
