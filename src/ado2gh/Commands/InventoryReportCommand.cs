using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.AdoToGithub.Commands
{
    public class InventoryReportCommand : Command
    {
        internal Func<string, string, Task> WriteToFile = async (path, contents) => await File.WriteAllTextAsync(path, contents);

        private readonly OctoLogger _log;
        private readonly AdoApiFactory _adoApiFactory;
        private readonly AdoInspectorServiceFactory _adoInspectorServiceFactory;
        private readonly OrgsCsvGeneratorService _orgsCsvGenerator;
        private readonly TeamProjectsCsvGeneratorService _teamProjectsCsvGenerator;
        private readonly ReposCsvGeneratorService _reposCsvGenerator;
        private readonly PipelinesCsvGeneratorService _pipelinesCsvGenerator;

        public InventoryReportCommand(
            OctoLogger log,
            AdoApiFactory adoApiFactory,
            AdoInspectorServiceFactory adoInspectorServiceFactory,
            OrgsCsvGeneratorService orgsCsvGeneratorService,
            TeamProjectsCsvGeneratorService teamProjectsCsvGeneratorService,
            ReposCsvGeneratorService reposCsvGeneratorService,
            PipelinesCsvGeneratorService pipelinesCsvGeneratorService) : base("inventory-report")
        {
            _log = log;
            _adoApiFactory = adoApiFactory;
            _adoInspectorServiceFactory = adoInspectorServiceFactory;
            _orgsCsvGenerator = orgsCsvGeneratorService;
            _teamProjectsCsvGenerator = teamProjectsCsvGeneratorService;
            _reposCsvGenerator = reposCsvGeneratorService;
            _pipelinesCsvGenerator = pipelinesCsvGeneratorService;

            Description = "Generates several CSV files containing lists of ADO orgs, team projects, repos, and pipelines. Useful for planning large migrations.";
            Description += Environment.NewLine;
            Description += "Note: Expects ADO_PAT env variable or --ado-pat option to be set.";

            var adoOrg = new Option<string>("--ado-org")
            {
                IsRequired = false,
                Description = "If not provided will iterate over all orgs that ADO_PAT has access to."
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
            AddOption(adoPat);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, bool>(Invoke);
        }

        public async Task Invoke(string adoOrg, string adoPat = null, bool verbose = false)
        {
            _log.Verbose = verbose;

            _log.LogInformation("Creating inventory report...");

            if (adoOrg.HasValue())
            {
                _log.LogInformation($"ADO ORG: {adoOrg}");
            }

            if (adoPat is not null)
            {
                _log.LogInformation("ADO PAT: ***");
            }

            var ado = _adoApiFactory.Create(adoPat);
            var inspector = _adoInspectorServiceFactory.Create(ado);
            inspector.OrgFilter = adoOrg;

            _log.LogInformation("Finding Orgs...");
            var orgs = await inspector.GetOrgs();
            _log.LogInformation($"Found {orgs.Count()} Orgs");

            _log.LogInformation("Finding Team Projects...");
            var teamProjectCount = await inspector.GetTeamProjectCount();
            _log.LogInformation($"Found {teamProjectCount} Team Projects");

            _log.LogInformation("Finding Repos...");
            var repoCount = await inspector.GetRepoCount();
            _log.LogInformation($"Found {repoCount} Repos");

            _log.LogInformation("Finding Pipelines...");
            var pipelineCount = await inspector.GetPipelineCount();
            _log.LogInformation($"Found {pipelineCount} Pipelines");

            _log.LogInformation("Generating orgs.csv...");
            var orgsCsvText = await _orgsCsvGenerator.Generate(adoPat);
            await WriteToFile("orgs.csv", orgsCsvText);
            _log.LogSuccess("orgs.csv generated");

            _log.LogInformation("Generating teamprojects.csv...");
            var teamProjectsCsvText = await _teamProjectsCsvGenerator.Generate(adoPat);
            await WriteToFile("team-projects.csv", teamProjectsCsvText);
            _log.LogSuccess("team-projects.csv generated");

            _log.LogInformation("Generating repos.csv...");
            var reposCsvText = await _reposCsvGenerator.Generate(adoPat);
            await WriteToFile("repos.csv", reposCsvText);
            _log.LogSuccess("repos.csv generated");

            _log.LogInformation("Generating pipelines.csv...");
            var pipelinesCsvText = await _pipelinesCsvGenerator.Generate(adoPat);
            await WriteToFile("pipelines.csv", pipelinesCsvText);
            _log.LogSuccess("pipelines.csv generated");
        }
    }
}
