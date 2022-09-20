using System;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.Threading.Tasks;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.AdoToGithub.Commands
{
    public class MigrateRepoCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly GithubApiFactory _githubApiFactory;
        private readonly EnvironmentVariableProvider _environmentVariableProvider;

        public MigrateRepoCommand(OctoLogger log, GithubApiFactory githubApiFactory, EnvironmentVariableProvider environmentVariableProvider) : base(
            name: "migrate-repo",
            description: "Invokes the GitHub API's to migrate the repo and all PR data" +
                         Environment.NewLine +
                         "Note: Expects ADO_PAT and GH_PAT env variables or --ado-pat and --github-pat options to be set.")
        {
            _log = log;
            _githubApiFactory = githubApiFactory;
            _environmentVariableProvider = environmentVariableProvider;

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
            var targetRepoVisibility = new Option<string>("--target-repo-visibility")
            {
                IsRequired = false,
                Description = "Defaults to private. Valid values are public, private, internal"
            };
            var wait = new Option<bool>("--wait")
            {
                IsRequired = false,
                Description = "Synchronously waits for the repo migration to finish."
            };
            var adoPat = new Option<string>("--ado-pat")
            {
                IsRequired = false
            };
            var githubPat = new Option<string>("--github-pat")
            {
                IsRequired = false
            };
            var verbose = new Option<bool>("--verbose")
            {
                IsRequired = false
            };

            AddOption(adoOrg);
            AddOption(adoTeamProject);
            AddOption(adoRepo);
            AddOption(githubOrg);
            AddOption(githubRepo);
            AddOption(targetRepoVisibility);
            AddOption(wait);
            AddOption(adoPat);
            AddOption(githubPat);
            AddOption(verbose);

            Handler = CommandHandler.Create<MigrateRepoCommandArgs>(Invoke);
        }

        public async Task Invoke(MigrateRepoCommandArgs args)
        {
            if (args is null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            _log.Verbose = args.Verbose;

            _log.LogInformation("Migrating Repo...");
            _log.LogInformation($"ADO ORG: {args.AdoOrg}");
            _log.LogInformation($"ADO TEAM PROJECT: {args.AdoTeamProject}");
            _log.LogInformation($"ADO REPO: {args.AdoRepo}");
            _log.LogInformation($"GITHUB ORG: {args.GithubOrg}");
            _log.LogInformation($"GITHUB REPO: {args.GithubRepo}");

            if (args.TargetRepoVisibility.HasValue())
            {
                _log.LogInformation($"TARGET REPO VISIBILITY: {args.TargetRepoVisibility}");
            }
            if (args.Wait)
            {
                _log.LogInformation("WAIT: true");
            }
            if (args.AdoPat is not null)
            {
                _log.LogInformation("ADO PAT: ***");
            }
            if (args.GithubPat is not null)
            {
                _log.LogInformation("GITHUB PAT: ***");
            }

            args.GithubPat ??= _environmentVariableProvider.GithubPersonalAccessToken();
            var githubApi = _githubApiFactory.Create(targetPersonalAccessToken: args.GithubPat);

            var adoRepoUrl = GetAdoRepoUrl(args.AdoOrg, args.AdoTeamProject, args.AdoRepo);

            args.AdoPat ??= _environmentVariableProvider.AdoPersonalAccessToken();
            var githubOrgId = await githubApi.GetOrganizationId(args.GithubOrg);
            var migrationSourceId = await githubApi.CreateAdoMigrationSource(githubOrgId, null);

            string migrationId;

            try
            {
                migrationId = await githubApi.StartMigration(migrationSourceId, adoRepoUrl, githubOrgId, args.GithubRepo, args.AdoPat, args.GithubPat, targetRepoVisibility: args.TargetRepoVisibility);
            }
            catch (OctoshiftCliException ex)
            {
                if (ex.Message == $"A repository called {args.GithubOrg}/{args.GithubRepo} already exists")
                {
                    _log.LogWarning($"The Org '{args.GithubOrg}' already contains a repository with the name '{args.GithubRepo}'. No operation will be performed");
                    return;
                }

                throw;
            }

            if (!args.Wait)
            {
                _log.LogInformation($"A repository migration (ID: {migrationId}) was successfully queued.");
                return;
            }

            var (migrationState, _, failureReason) = await githubApi.GetMigration(migrationId);

            while (RepositoryMigrationStatus.IsPending(migrationState))
            {
                _log.LogInformation($"Migration in progress (ID: {migrationId}). State: {migrationState}. Waiting 10 seconds...");
                await Task.Delay(10000);
                (migrationState, _, failureReason) = await githubApi.GetMigration(migrationId);
            }

            if (RepositoryMigrationStatus.IsFailed(migrationState))
            {
                _log.LogError($"Migration Failed. Migration ID: {migrationId}");
                throw new OctoshiftCliException(failureReason);
            }

            _log.LogSuccess($"Migration completed (ID: {migrationId})! State: {migrationState}");
        }

        private string GetAdoRepoUrl(string org, string project, string repo) => $"https://dev.azure.com/{org}/{project}/_git/{repo}".Replace(" ", "%20");
    }

    public class MigrateRepoCommandArgs
    {
        public string AdoOrg { get; set; }
        public string AdoTeamProject { get; set; }
        public string AdoRepo { get; set; }
        public string GithubOrg { get; set; }
        public string GithubRepo { get; set; }
        public string TargetRepoVisibility { get; set; }
        public bool Wait { get; set; }
        public string AdoPat { get; set; }
        public string GithubPat { get; set; }
        public bool Verbose { get; set; }
    }
}
