using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OctoshiftCLI.AdoToGithub.Commands
{
    public class InventoryReportCommand : Command
    {
        internal Func<string, string, Task> WriteToFile = async (path, contents) => await File.WriteAllTextAsync(path, contents);

        private readonly OctoLogger _log;
        private readonly AdoApiFactory _adoApiFactory;
        private readonly AdoInspectorService _adoInspectorService;
        private readonly OrgsCsvGeneratorService _orgsCsvGenerator;
        private readonly TeamProjectsCsvGeneratorService _teamProjectsCsvGenerator;
        private readonly ReposCsvGeneratorService _reposCsvGenerator;
        private readonly PipelinesCsvGeneratorService _pipelinesCsvGenerator;

        public InventoryReportCommand(OctoLogger log, AdoApiFactory adoApiFactory, AdoInspectorService adoInspectorService, OrgsCsvGeneratorService orgsCsvGeneratorService, TeamProjectsCsvGeneratorService teamProjectsCsvGeneratorService, ReposCsvGeneratorService reposCsvGeneratorService, PipelinesCsvGeneratorService pipelinesCsvGeneratorService) : base("inventory-report")
        {
            _log = log;
            _adoApiFactory = adoApiFactory;
            _adoInspectorService = adoInspectorService;
            _orgsCsvGenerator = orgsCsvGeneratorService;
            _teamProjectsCsvGenerator = teamProjectsCsvGeneratorService;
            _reposCsvGenerator = reposCsvGeneratorService;
            _pipelinesCsvGenerator = pipelinesCsvGeneratorService;

            Description = "Generates several CSV files containing lists of ADO orgs, team projects, repos, and pipelines. Useful for planning large migrations. The repos.csv can be fed as an input into other commands to help splitting large migrations up into batches.";
            Description += Environment.NewLine;
            Description += "Note: Expects ADO_PAT env variable or --ado-pat option to be set.";

            IsHidden = true;

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

            if (!string.IsNullOrWhiteSpace(adoOrg))
            {
                _log.LogInformation($"ADO ORG: {adoOrg}");
            }

            if (adoPat is not null)
            {
                _log.LogInformation("ADO PAT: ***");
            }

            var ado = _adoApiFactory.Create(adoPat);

            var orgs = await _adoInspectorService.GetOrgs(ado, adoOrg);
            _log.LogInformation($"Found {orgs?.Count()} Orgs");

            var teamProjects = await _adoInspectorService.GetTeamProjects(ado, orgs);
            _log.LogInformation($"Found {teamProjects?.Sum(org => org.Value.Count())} Team Projects");

            var repos = await _adoInspectorService.GetRepos(ado, teamProjects);
            _log.LogInformation($"Found {repos?.Sum(org => org.Value.Sum(tp => tp.Value.Count()))} Repos");

            var pipelines = await _adoInspectorService.GetPipelines(ado, repos);
            _log.LogInformation($"Found {pipelines?.Sum(org => org.Value.Sum(tp => tp.Value.Sum(repo => repo.Value.Count())))} Pipelines");

            var orgsCsvText = await _orgsCsvGenerator.Generate(ado, pipelines);
            var teamProjectsCsvText = _teamProjectsCsvGenerator.Generate(pipelines);
            var reposCsvText = _reposCsvGenerator.Generate(pipelines);
            var pipelinesCsvText = await _pipelinesCsvGenerator.Generate(ado, pipelines);

            await WriteToFile("orgs.csv", orgsCsvText);
            _log.LogSuccess("orgs.csv generated");

            await WriteToFile("team-projects.csv", teamProjectsCsvText);
            _log.LogSuccess("team-projects.csv generated");

            await WriteToFile("repos.csv", reposCsvText);
            _log.LogSuccess("repos.csv generated");

            await WriteToFile("pipelines.csv", pipelinesCsvText);
            _log.LogSuccess("pipelines.csv generated");
        }
    }
}
