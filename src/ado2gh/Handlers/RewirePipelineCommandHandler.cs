using System;
using System.Threading.Tasks;
using OctoshiftCLI.AdoToGithub.Commands;

namespace OctoshiftCLI.AdoToGithub.Handlers
{
    public class RewirePipelineCommandHandler
    {
        private readonly OctoLogger _log;
        private readonly AdoApiFactory _adoApiFactory;

        public RewirePipelineCommandHandler(OctoLogger log, AdoApiFactory adoApiFactory)
        {
            _log = log;
            _adoApiFactory = adoApiFactory;
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
}
