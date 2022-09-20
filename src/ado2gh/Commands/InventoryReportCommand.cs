using System;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;

namespace OctoshiftCLI.AdoToGithub.Commands
{
    public class InventoryReportCommand : Command
    {
        public InventoryReportCommand(
            OctoLogger log,
            AdoApiFactory adoApiFactory,
            AdoInspectorServiceFactory adoInspectorServiceFactory,
            OrgsCsvGeneratorService orgsCsvGeneratorService,
            TeamProjectsCsvGeneratorService teamProjectsCsvGeneratorService,
            ReposCsvGeneratorService reposCsvGeneratorService,
            PipelinesCsvGeneratorService pipelinesCsvGeneratorService) : base(
                name: "inventory-report",
                description: "Generates several CSV files containing lists of ADO orgs, team projects, repos, and pipelines. Useful for planning large migrations." +
                             Environment.NewLine +
                             "Note: Expects ADO_PAT env variable or --ado-pat option to be set.")
        {
            var adoOrg = new Option<string>("--ado-org")
            {
                IsRequired = false,
                Description = "If not provided will iterate over all orgs that ADO_PAT has access to."
            };
            var adoPat = new Option<string>("--ado-pat")
            {
                IsRequired = false
            };
            var minimal = new Option<bool>("--minimal")
            {
                IsRequired = false,
                Description = "Significantly speeds up the generation of the CSV files by including the bare minimum info."
            };
            var verbose = new Option<bool>("--verbose")
            {
                IsRequired = false
            };

            AddOption(adoOrg);
            AddOption(adoPat);
            AddOption(minimal);
            AddOption(verbose);

            var handler = new InventoryReportCommandHandler(
                log,
                adoApiFactory,
                adoInspectorServiceFactory,
                orgsCsvGeneratorService,
                teamProjectsCsvGeneratorService,
                reposCsvGeneratorService,
                pipelinesCsvGeneratorService);
            Handler = CommandHandler.Create<InventoryReportCommandArgs>(handler.Invoke);
        }
    }

    public class InventoryReportCommandArgs
    {
        public string AdoOrg { get; set; }
        public string AdoPat { get; set; }
        public bool Minimal { get; set; }
        public bool Verbose { get; set; }
    }
}
