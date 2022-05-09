using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Net.Http;
using System.IO;

[assembly: InternalsVisibleTo("OctoshiftCLI.Tests")]
namespace OctoshiftCLI.GithubEnterpriseImporter.Commands
{
    public class DownloadLogsCommand : Command
    {
        private readonly OctoLogger _log;

        public DownloadLogsCommand(OctoLogger log, ITargetGithubApiFactory targetGithubApiFactory) : base("download-logs")
        {
            _log = log;

            Description = "Downloads migration logs for migrations.";

            var org = new Option<string>("--github-org")
            {
                IsRequired = true,
                Description = "GitHub organization to download migration logs for."
            };

            var repo = new Option<string>("--repo")
            {
                IsRequired = true,
                Description = "GitHub repository to download migration log for."
            };

            var verbose = new Option("--verbose") { IsRequired = false };

            AddOption(org);
            AddOption(repo);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, bool>(Invoke);
        }

        public async Task Invoke(string org, string repo, bool verbose = false)
        {
            _log.Verbose = verbose;

            _log.LogInformation($"Downloading logs for organization {org}...");

            var logFile = $"migration-log-{org}-{repo}";

            _log.LogInformation($"Downloading log for repository {repo} to {logFile}...");

            HttpClient client = new HttpClient();

            try
            {
              // FIXME use real URL here
              using HttpResponseMessage response = await client.GetAsync("http://localhost:8080/migration-log", HttpCompletionOption.ResponseHeadersRead);
              using Stream streamToReadFrom = await response.Content.ReadAsStreamAsync(); 
              using Stream streamToWriteTo = File.Open(logFile, FileMode.Create); 
              await streamToReadFrom.CopyToAsync(streamToWriteTo);

              _log.LogInformation($"Downloaded {repo} log to {logFile}.");
            }
            catch (HttpRequestException)
            {
              _log.LogInformation($"Could not download log for repository {repo}!");
            }
            finally
            {
              client.Dispose();
            }
        }
    }
}
