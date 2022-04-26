using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands
{
    public class GenerateReclaimCsvCommand : Command
    {
        internal Func<string, string, Task> WriteToFile = async (path, contents) => await File.WriteAllTextAsync(path, contents);

        private readonly OctoLogger _log;
        private readonly ITargetGithubApiFactory _targetGithubApiFactory;

        public GenerateReclaimCsvCommand(OctoLogger log, ITargetGithubApiFactory targetGithubApiFactory) : base("generate-reclaim-csv")
        {
            _log = log;
            _targetGithubApiFactory = targetGithubApiFactory;

            Description = "Generates a CSV with unreclained mannequins to reclaim them in bulk.";

            var githubTargetOrgOption = new Option<string>("--github-target-org")
            {
                IsRequired = true,
                Description = "Uses GH_PAT env variable."
            };

            var outputOption = new Option<string>("--output")
            {
                IsRequired = true,
                Description = "Output filename"
            };

            var forceOption = new Option("--include-reclaimed")
            {
                IsRequired = false,
                Description = "Map the user even if it was previously mapped"
            };

            var verbose = new Option("--verbose")
            {
                IsRequired = false
            };

            var githubTargetPat = new Option<string>("--github-target-pat")
            {
                IsRequired = false
            };

            AddOption(githubTargetOrgOption);
            AddOption(outputOption);
            AddOption(forceOption);
            AddOption(githubTargetPat);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, bool, string, bool>(Invoke);
        }

        public async Task Invoke(
          string githubTargetOrg,
          string output,
          bool includeReclaimed = false,
          string githubTargetPat = null,
          bool verbose = false)
        {
            _log.Verbose = verbose;

            _log.LogInformation("Generating CSV...");

            _log.LogInformation($"GITHUB TARGET ORG: {githubTargetOrg}");
            if (githubTargetPat is not null)
            {
                _log.LogInformation("GITHUB TARGET PAT: ***");
            }
            _log.LogInformation($"FILE: {output}");
            if (includeReclaimed)
            {
                _log.LogInformation("INCLUDING RECLAIMED");
            }

            var githubApi = _targetGithubApiFactory.Create(targetPersonalAccessToken: githubTargetPat);

            var githubOrgId = await githubApi.GetOrganizationId(githubTargetOrg);
            _log.LogInformation($"    Organization Id: {githubOrgId}");

            var mannequins = await githubApi.GetMannequins(githubOrgId);

            _log.LogInformation($"    # Mannequins Found: {mannequins.Count()}");

            var numberMannequins = 0;
            var contents = new StringBuilder().AppendLine("login,claimantlogin");
            foreach (var mannequin in mannequins.Where(m => includeReclaimed || m.MappedUser is null))
            {
                numberMannequins++;
                contents.AppendLine($"{mannequin.Login},{mannequin.MappedUser?.Login}");
            }

            await WriteToFile(output, contents.ToString());

            _log.LogInformation($"    # Mannequins Included: {numberMannequins}");
        }
    }
}
