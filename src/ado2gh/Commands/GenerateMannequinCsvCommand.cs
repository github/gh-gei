using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OctoshiftCLI.AdoToGithub.Commands
{
    public class GenerateMannequinCsvCommand : Command
    {
        internal Func<string, string, Task> WriteToFile = async (path, contents) => await File.WriteAllTextAsync(path, contents);

        private readonly OctoLogger _log;
        private readonly GithubApiFactory _githubApiFactory;

        public GenerateMannequinCsvCommand(OctoLogger log, GithubApiFactory githubApiFactory) : base("generate-mannequin-csv")
        {
            _log = log;
            _githubApiFactory = githubApiFactory;

            Description = "Generates a CSV with unreclaimed mannequins to reclaim them in bulk.";
            IsHidden = true;

            var githubOrgOption = new Option<string>("--github-org")
            {
                IsRequired = true,
                Description = "Uses GH_PAT env variable."
            };

            var outputOption = new Option<FileInfo>("--output", () => new FileInfo("./mannequins.csv"))
            {
                IsRequired = false,
                Description = "Output filename. Default value mannequins.csv"
            };

            var includeReclaimedOption = new Option("--include-reclaimed")
            {
                IsRequired = false,
                Description = "Include mannequins that have already been reclaimed"
            };

            var verbose = new Option("--verbose")
            {
                IsRequired = false
            };

            var githubPat = new Option<string>("--github-pat")
            {
                IsRequired = false
            };

            AddOption(githubOrgOption);
            AddOption(outputOption);
            AddOption(includeReclaimedOption);
            AddOption(githubPat);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, FileInfo, bool, string, bool>(Invoke);
        }

        public async Task Invoke(
          string githubOrg,
          FileInfo output,
          bool includeReclaimed = false,
          string githubPat = null,
          bool verbose = false)
        {
            _log.Verbose = verbose;

            _log.LogInformation("Generating CSV...");

            _log.LogInformation($"GITHUB ORG: {githubOrg}");
            if (githubPat is not null)
            {
                _log.LogInformation("GITHUB PAT: ***");
            }
            _log.LogInformation($"FILE: {output}");
            if (includeReclaimed)
            {
                _log.LogInformation("INCLUDING RECLAIMED");
            }

            var githubApi = _githubApiFactory.Create(personalAccessToken: githubPat);

            var githubOrgId = await githubApi.GetOrganizationId(githubOrg);
            var mannequins = await githubApi.GetMannequins(githubOrgId);

            _log.LogInformation($"    # Mannequins Found: {mannequins.Count()}");

            var numberMannequins = 0;
            var contents = new StringBuilder().AppendLine("login,claimantlogin");
            foreach (var mannequin in mannequins.Where(m => includeReclaimed || m.MappedUser is null))
            {
                numberMannequins++;
                contents.AppendLine($"{mannequin.Login},{mannequin.MappedUser?.Login}");
            }

            if (output?.FullName is not null)
            {
                await WriteToFile(output.FullName, contents.ToString());
            }

            _log.LogInformation($"    # Mannequins Included: {numberMannequins}");
        }
    }
}
