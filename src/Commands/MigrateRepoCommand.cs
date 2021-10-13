using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace OctoshiftCLI.Commands
{
    public class MigrateRepoCommand : Command
    {
        private GithubApi _github;

        public MigrateRepoCommand() : base("migrate-repo")
        {
            var adoOrg = new Option<string>("--ado-org")
            {
                IsRequired = true
            };
            var adoTeamProject = new Option<string>("--ado-team-project")
            {
                IsRequired = true
            };
            var adoRepo = new Option<string>("--ado-repo")
            {
                IsRequired = true
            };
            var githubOrg = new Option<string>("--github-org")
            {
                IsRequired = true
            };
            var githubRepo = new Option<string>("--github-repo")
            {
                IsRequired = true
            };

            AddOption(adoOrg);
            AddOption(adoTeamProject);
            AddOption(adoRepo);
            AddOption(githubOrg);
            AddOption(githubRepo);

            Handler = CommandHandler.Create<string, string, string, string, string>(Invoke);
        }

        private async Task Invoke(string adoOrg, string adoTeamProject, string adoRepo, string githubOrg, string githubRepo)
        {
            Console.WriteLine("Migrating Repo...");
            Console.WriteLine($"ADO ORG: {adoOrg}");
            Console.WriteLine($"ADO TEAM PROJECT: {adoTeamProject}");
            Console.WriteLine($"ADO REPO: {adoRepo}");
            Console.WriteLine($"GITHUB ORG: {githubOrg}");
            Console.WriteLine($"GITHUB REPO: {githubRepo}");

            var adoToken = Environment.GetEnvironmentVariable("ADO_PAT");

            if (string.IsNullOrWhiteSpace(adoToken))
            {
                Console.WriteLine("ERROR: NO ADO_PAT FOUND IN ENV VARS, exiting...");
                return;
            }

            var githubToken = Environment.GetEnvironmentVariable("GH_PAT");

            if (string.IsNullOrWhiteSpace(githubToken))
            {
                Console.WriteLine("ERROR: NO GH_PAT FOUND IN ENV VARS, exiting...");
                return;
            }

            _github = new GithubApi(githubToken);

            var githubOrgId = await _github.GetOrganizationId(githubOrg);
            var migrationSourceId = await _github.CreateMigrationSource(githubOrgId, adoToken);

        }
    }
}
