using System;
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.AdoToGithub.Factories;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.AdoToGithub.Commands.InventoryReport
{
    public class InventoryReportCommand : CommandBase<InventoryReportCommandArgs, InventoryReportCommandHandler>
    {
        public InventoryReportCommand() : base(
                name: "inventory-report",
                description: "Generates several CSV files containing lists of ADO orgs, team projects, repos, and pipelines. Useful for planning large migrations." +
                             Environment.NewLine +
                             "Note: Expects ADO_PAT env variable or --ado-pat option to be set.")
        {
            AddOption(AdoOrg);
            AddOption(AdoPat);
            AddOption(Minimal);
            AddOption(Verbose);
        }

        public Option<string> AdoOrg { get; } = new("--ado-org")
        {
            Description = "If not provided will iterate over all orgs that ADO_PAT has access to."
        };
        public Option<string> AdoPat { get; } = new("--ado-pat");
        public Option<bool> Minimal { get; } = new("--minimal")
        {
            Description = "Significantly speeds up the generation of the CSV files by including the bare minimum info."
        };
        public Option<bool> Verbose { get; } = new("--verbose");

        public override InventoryReportCommandHandler BuildHandler(InventoryReportCommandArgs args, IServiceProvider sp)
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
            var adoInspectorServiceFactory = sp.GetRequiredService<AdoInspectorServiceFactory>();
            var adoInspectorService = adoInspectorServiceFactory.Create(adoApi);
            var orgsCsvGeneratorService = sp.GetRequiredService<OrgsCsvGeneratorService>();
            var teamProjectsCsvGeneratorService = sp.GetRequiredService<TeamProjectsCsvGeneratorService>();
            var reposCsvGeneratorService = sp.GetRequiredService<ReposCsvGeneratorService>();
            var pipelinesCsvGeneratorService = sp.GetRequiredService<PipelinesCsvGeneratorService>();

            return new InventoryReportCommandHandler(
                log,
                adoInspectorService,
                orgsCsvGeneratorService,
                teamProjectsCsvGeneratorService,
                reposCsvGeneratorService,
                pipelinesCsvGeneratorService);
        }
    }
}
