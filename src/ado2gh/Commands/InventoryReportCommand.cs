using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
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

        public InventoryReportCommand(OctoLogger log, AdoApiFactory adoApiFactory, AdoInspectorService adoInspectorService, OrgsCsvGeneratorService orgsCsvGeneratorService) : base("inventory-report")
        {
            _log = log;
            _adoApiFactory = adoApiFactory;
            _orgsCsvGenerator = orgsCsvGeneratorService;
            _adoInspectorService = adoInspectorService;

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

        public async Task Invoke(string adoOrg, string adoPat, bool verbose = false)
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

            var orgsCsvText = await _orgsCsvGenerator.Generate(ado, orgs);

            await WriteToFile("orgs.csv", orgsCsvText);
            _log.LogSuccess("orgs.csv generated");
        }
    }
}
