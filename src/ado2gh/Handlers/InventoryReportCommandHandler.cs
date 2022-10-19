using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OctoshiftCLI.AdoToGithub.Commands;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Handlers;

namespace OctoshiftCLI.AdoToGithub.Handlers;

public class InventoryReportCommandHandler : ICommandHandler<InventoryReportCommandArgs>
{
    internal Func<string, string, Task> WriteToFile = async (path, contents) => await File.WriteAllTextAsync(path, contents);

    private readonly OctoLogger _log;
    private readonly AdoInspectorService _adoInspectorService;
    private readonly OrgsCsvGeneratorService _orgsCsvGenerator;
    private readonly TeamProjectsCsvGeneratorService _teamProjectsCsvGenerator;
    private readonly ReposCsvGeneratorService _reposCsvGenerator;
    private readonly PipelinesCsvGeneratorService _pipelinesCsvGenerator;

    public InventoryReportCommandHandler(
        OctoLogger log,
        AdoInspectorService adoInspectorService,
        OrgsCsvGeneratorService orgsCsvGeneratorService,
        TeamProjectsCsvGeneratorService teamProjectsCsvGeneratorService,
        ReposCsvGeneratorService reposCsvGeneratorService,
        PipelinesCsvGeneratorService pipelinesCsvGeneratorService)
    {
        _log = log;
        _adoInspectorService = adoInspectorService;
        _orgsCsvGenerator = orgsCsvGeneratorService;
        _teamProjectsCsvGenerator = teamProjectsCsvGeneratorService;
        _reposCsvGenerator = reposCsvGeneratorService;
        _pipelinesCsvGenerator = pipelinesCsvGeneratorService;
    }

    public async Task Handle(InventoryReportCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.Verbose = args.Verbose;

        _log.LogInformation("Creating inventory report...");

        if (args.AdoOrg.HasValue())
        {
            _log.LogInformation($"ADO ORG: {args.AdoOrg}");
        }

        if (args.AdoPat is not null)
        {
            _log.LogInformation("ADO PAT: ***");
        }

        if (args.Minimal)
        {
            _log.LogInformation("MINIMAL: true");
        }

        _log.RegisterSecret(args.AdoPat);

        _adoInspectorService.OrgFilter = args.AdoOrg;

        _log.LogInformation("Finding Orgs...");
        var orgs = await _adoInspectorService.GetOrgs();
        _log.LogInformation($"Found {orgs.Count()} Orgs");

        _log.LogInformation("Finding Team Projects...");
        var teamProjectCount = await _adoInspectorService.GetTeamProjectCount();
        _log.LogInformation($"Found {teamProjectCount} Team Projects");

        _log.LogInformation("Finding Repos...");
        var repoCount = await _adoInspectorService.GetRepoCount();
        _log.LogInformation($"Found {repoCount} Repos");

        _log.LogInformation("Finding Pipelines...");
        var pipelineCount = await _adoInspectorService.GetPipelineCount();
        _log.LogInformation($"Found {pipelineCount} Pipelines");

        _log.LogInformation("Generating orgs.csv...");
        var orgsCsvText = await _orgsCsvGenerator.Generate(args.AdoPat, args.Minimal);
        await WriteToFile("orgs.csv", orgsCsvText);
        _log.LogSuccess("orgs.csv generated");

        _log.LogInformation("Generating teamprojects.csv...");
        var teamProjectsCsvText = await _teamProjectsCsvGenerator.Generate(args.AdoPat, args.Minimal);
        await WriteToFile("team-projects.csv", teamProjectsCsvText);
        _log.LogSuccess("team-projects.csv generated");

        _log.LogInformation("Generating repos.csv...");
        var reposCsvText = await _reposCsvGenerator.Generate(args.AdoPat, args.Minimal);
        await WriteToFile("repos.csv", reposCsvText);
        _log.LogSuccess("repos.csv generated");

        _log.LogInformation("Generating pipelines.csv...");
        var pipelinesCsvText = await _pipelinesCsvGenerator.Generate(args.AdoPat);
        await WriteToFile("pipelines.csv", pipelinesCsvText);
        _log.LogSuccess("pipelines.csv generated");
    }
}
