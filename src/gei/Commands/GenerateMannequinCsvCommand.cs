using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands
{
    public class GenerateMannequinCsvCommand : Command
    {
        internal Func<string, string, Task> WriteToFile = async (path, contents) => await File.WriteAllTextAsync(path, contents);

        private readonly OctoLogger _log;
        private readonly ITargetGithubApiFactory _targetGithubApiFactory;

        public GenerateMannequinCsvCommand(OctoLogger log, ITargetGithubApiFactory targetGithubApiFactory) : base("generate-mannequin-csv")
        {
            _log = log;
            _targetGithubApiFactory = targetGithubApiFactory;

            Description = "Generates a CSV with unreclaimed mannequins to reclaim them in bulk.";
            IsHidden = true;

            var githubTargetOrgOption = new Option<string>("--github-target-org")
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

            var githubTargetPat = new Option<string>("--github-target-pat")
            {
                IsRequired = false
            };

            AddOption(githubTargetOrgOption);
            AddOption(outputOption);
            AddOption(includeReclaimedOption);
            AddOption(githubTargetPat);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, FileInfo, bool, string, bool>(Invoke);
        }

        public async Task Invoke(
          string githubTargetOrg,
          FileInfo output,
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

            var githubApi = _targetGithubApiFactory.Create(githubTargetPat, Name);

            var githubOrgId = await githubApi.GetOrganizationId(githubTargetOrg);
            var mannequins = await githubApi.GetMannequins(githubOrgId);

            _log.LogInformation($"    # Mannequins Found: {mannequins.Count()}");
            _log.LogInformation($"    # Mannequins Previously Reclaimed: {mannequins.Count(x => x.MappedUser is not null)}");

            var contents = new StringBuilder().AppendLine("mannequin-user,mannequin-id,target-user");
            foreach (var mannequin in mannequins.Where(m => includeReclaimed || m.MappedUser is null))
            {
                contents.AppendLine($"{mannequin.Login},{mannequin.Id},{mannequin.MappedUser?.Login}");
            }

            if (output?.FullName is not null)
            {
                await WriteToFile(output.FullName, contents.ToString());
            }
        }
    }
}
