using System;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.Threading.Tasks;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands
{
    public class MigrateOrgCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly ITargetGithubApiFactory _targetGithubApiFactory;
        private readonly EnvironmentVariableProvider _environmentVariableProvider;
        private const string DEFAULT_GITHUB_BASE_URL = "https://github.com";

        public MigrateOrgCommand(OctoLogger log, ITargetGithubApiFactory targetGithubApiFactory, EnvironmentVariableProvider environmentVariableProvider) : base(
            name: "migrate-org",
            description: "Invokes the GitHub APIs to migrate a GitHub org with its teams and the repositories.")
        {
            IsHidden = true;
            _log = log;
            _targetGithubApiFactory = targetGithubApiFactory;
            _environmentVariableProvider = environmentVariableProvider;

            var githubSourceOrg = new Option<string>("--github-source-org")
            {
                IsRequired = true,
                Description = "Uses GH_SOURCE_PAT env variable or --github-source-pat option. Will fall back to GH_PAT or --github-target-pat if not set."
            };
            var githubTargetOrg = new Option<string>("--github-target-org")
            {
                IsRequired = true,
                Description = "Uses GH_PAT env variable or --github-target-pat option."
            };
            var githubTargetEnterprise = new Option<string>("--github-target-enterprise")
            {
                IsRequired = true,
                Description = "Name of the target enterprise."
            };
            var githubSourcePat = new Option<string>("--github-source-pat")
            {
                IsRequired = false
            };
            var githubTargetPat = new Option<string>("--github-target-pat")
            {
                IsRequired = false
            };
            var wait = new Option<bool>("--wait")
            {
                IsRequired = false,
                Description = "Synchronously waits for the org migration to finish."
            };
            var verbose = new Option<bool>("--verbose")
            {
                IsRequired = false
            };

            AddOption(githubSourceOrg);
            AddOption(githubTargetOrg);
            AddOption(githubTargetEnterprise);

            AddOption(githubSourcePat);
            AddOption(githubTargetPat);
            AddOption(wait);
            AddOption(verbose);

            Handler = CommandHandler.Create<MigrateOrgCommandArgs>(Invoke);
        }

        public async Task Invoke(MigrateOrgCommandArgs args)
        {
            if (args is null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            _log.Verbose = args.Verbose;

            LogAndValidateOptions(args);

            _log.RegisterSecret(args.GithubSourcePat);
            _log.RegisterSecret(args.GithubTargetPat);

            var githubApi = _targetGithubApiFactory.Create(targetPersonalAccessToken: args.GithubTargetPat);

            var githubEnterpriseId = await githubApi.GetEnterpriseId(args.GithubTargetEnterprise);
            var sourceOrgUrl = GetGithubOrgUrl(args.GithubSourceOrg, null);
            var sourceToken = GetSourceToken(args);
            var targetToken = args.GithubTargetPat ?? _environmentVariableProvider.TargetGithubPersonalAccessToken();

            var migrationId = await githubApi.StartOrganizationMigration(
                sourceOrgUrl,
                args.GithubTargetOrg,
                githubEnterpriseId,
                sourceToken,
                targetToken);


            if (!args.Wait)
            {
                _log.LogInformation($"A organization migration (ID: {migrationId}) was successfully queued.");
                return;
            }

            var migrationState = await githubApi.GetOrganizationMigrationState(migrationId);

            while (OrganizationMigrationStatus.IsPending(migrationState))
            {
                _log.LogInformation($"Migration in progress (ID: {migrationId}). State: {migrationState}. Waiting 10 seconds...");
                await Task.Delay(10000);
                migrationState = await githubApi.GetOrganizationMigrationState(migrationId);
            }

            if (OrganizationMigrationStatus.IsFailed(migrationState))
            {
                _log.LogError($"Migration Failed. Migration ID: {migrationId}");
                throw new OctoshiftCliException($"Migration Failed.");
            }

            _log.LogSuccess($"Migration completed (ID: {migrationId})! State: {migrationState}");
        }

        private string GetSourceToken(MigrateOrgCommandArgs args) =>
            args.GithubSourcePat ?? _environmentVariableProvider.SourceGithubPersonalAccessToken();

        private string GetGithubOrgUrl(string org, string baseUrl) => $"{baseUrl ?? DEFAULT_GITHUB_BASE_URL}/{org}".Replace(" ", "%20");

        private void LogAndValidateOptions(MigrateOrgCommandArgs args)
        {
            _log.LogInformation("Migrating Org...");
            _log.LogInformation($"GITHUB SOURCE ORG: {args.GithubSourceOrg}");
            _log.LogInformation($"GITHUB TARGET ORG: {args.GithubTargetOrg}");
            _log.LogInformation($"GITHUB TARGET ENTERPRISE: {args.GithubTargetEnterprise}");

            if (args.GithubSourcePat.HasValue())
            {
                _log.LogInformation("GITHUB SOURCE PAT: ***");
            }

            if (args.GithubTargetPat.HasValue())
            {
                _log.LogInformation("GITHUB TARGET PAT: ***");

                if (args.GithubSourcePat.IsNullOrWhiteSpace())
                {
                    args.GithubSourcePat = args.GithubTargetPat;
                    _log.LogInformation("Since github-target-pat is provided, github-source-pat will also use its value.");
                }
            }
        }
    }

    public class MigrateOrgCommandArgs
    {
        public string GithubSourceOrg { get; set; }
        public string GithubTargetOrg { get; set; }
        public string GithubTargetEnterprise { get; set; }
        public bool Wait { get; set; }
        public bool Verbose { get; set; }
        public string GithubSourcePat { get; set; }
        public string GithubTargetPat { get; set; }
    }
}
