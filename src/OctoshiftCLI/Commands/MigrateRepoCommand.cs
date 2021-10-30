using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace OctoshiftCLI.Commands
{
    public class MigrateRepoCommand : Command
    {
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

        public async Task Invoke(string adoOrg, string adoTeamProject, string adoRepo, string githubOrg, string githubRepo)
        {
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

            using var github = GithubApiFactory.Create(githubToken);

            await MigrateRepo(adoOrg, adoTeamProject, adoRepo, githubOrg, githubRepo, adoToken, github);
        }

        private async Task MigrateRepo(string adoOrg, string adoTeamProject, string adoRepo, string githubOrg, string githubRepo, string adoToken, GithubApi github)
        {
            Console.WriteLine("Migrating Repo...");
            Console.WriteLine($"ADO ORG: {adoOrg}");
            Console.WriteLine($"ADO TEAM PROJECT: {adoTeamProject}");
            Console.WriteLine($"ADO REPO: {adoRepo}");
            Console.WriteLine($"GITHUB ORG: {githubOrg}");
            Console.WriteLine($"GITHUB REPO: {githubRepo}");

            var adoRepoUrl = GetAdoRepoUrl(adoOrg, adoTeamProject, adoRepo);

            var githubOrgId = await github.GetOrganizationId(githubOrg);
            var migrationSourceId = await github.CreateMigrationSource(githubOrgId, adoToken);
            var migrationId = await github.StartMigration(migrationSourceId, adoRepoUrl, githubOrgId, githubRepo);

            var migrationState = await github.GetMigrationState(migrationId);

            while (migrationState.Trim().ToUpper() is "IN_PROGRESS" or "QUEUED")
            {
                Console.WriteLine($"Migration in progress (ID: {migrationId}). State: {migrationState}. Waiting 10 seconds...");
                await Task.Delay(10000);
                migrationState = await github.GetMigrationState(migrationId);
            }

            if (migrationState.Trim().ToUpper() == "FAILED")
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR: Migration Failed. Migration ID: {migrationId}");
                var failureReason = await github.GetMigrationFailureReason(migrationId);
                Console.WriteLine(failureReason);
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine($"Migration completed (ID: {migrationId})! State: {migrationState}");
            }
        }

        private string GetAdoRepoUrl(string org, string project, string repo) => $"https://dev.azure.com/{org}/{project}/_git/{repo}".Replace(" ", "%20");
    }
}