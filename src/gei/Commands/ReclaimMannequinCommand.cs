using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands
{
    public class ReclaimMannequinCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly ITargetGithubApiFactory _targetGithubApiFactory;


        private readonly EnvironmentVariableProvider _environmentVariableProvider;

        public ReclaimMannequinCommand(OctoLogger log, ITargetGithubApiFactory targetGithubApiFactory, EnvironmentVariableProvider environmentVariableProvider) : base("reclaim-mannequin")
        {
            _log = log;
            _targetGithubApiFactory = targetGithubApiFactory;
            _environmentVariableProvider = environmentVariableProvider;

            Description = "Reclaims a mannequin user. An invite will be sent and the user will have to accept for the remapping to occur.";

            var githubTargetOrgOption = new Option<string>("--github-target-org")
            {
                IsRequired = true,
                Description = "Uses GH_PAT env variable."
            };

            var mannequinUsernameOption = new Option<string>("--mannequin-user")
            {
                IsRequired = true,
                Description = "The login of the mannequin to be remapped."
            };
            var targetUsernameOption = new Option<string>("--target-user")
            {
                IsRequired = true,
                Description = "The login of the target user to be mapped."
            };

            var forceOption = new Option("--force")
            {
                IsRequired = false,
                Description = "Map the user even if it was previously mapped"
            };


            var verbose = new Option("--verbose")
            {
                IsRequired = false
            };

            AddOption(githubTargetOrgOption);
            AddOption(mannequinUsernameOption);
            AddOption(targetUsernameOption);
            AddOption(forceOption);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, string, bool, bool>(Invoke);
        }

        public async Task<bool> Invoke(
          string githubTargetOrg,
          string mannequinUser,
          string targetUser,
          bool force = false,
          bool verbose = false)
        {
            const string targetApiUrl = "https://api.github.com";

            _log.Verbose = verbose;

            _log.LogInformation("Reclaming Mannequin...");

            _log.LogInformation($"GITHUB TARGET ORG: {githubTargetOrg}");
            _log.LogInformation($"Mannequin: {mannequinUser}");
            _log.LogInformation($"Reclaiming User: {targetUser}");

            var githubApi = _targetGithubApiFactory.Create(targetApiUrl);
            var githubOrgId = await GetOrgId(githubApi, githubTargetOrg);

            var mannequin = await githubApi.GetMannequin(githubOrgId, mannequinUser);

            if (mannequin == null || mannequin.Id == null)
            {
                _log.LogError($"User {mannequinUser} is not a mannequin.");
                return false;
            }

            if (mannequin.MappedUser != null && force == false)
            {
                _log.LogError($"User {mannequinUser} has been already mapped to {mannequin.MappedUser.Login}. Use the force option if you want to reclaim the mannequin again.");
                return false;
            }

            var targetUserId = await githubApi.GetUserId(targetUser);

            if (targetUserId == null)
            {
                _log.LogError($"Target user {targetUser} not found.");
                return false;
            }

            var reclaimed = await githubApi.ReclaimMannequin(githubOrgId, mannequin.Id, targetUserId);

            if (reclaimed)
            {
                _log.LogInformation($"{mannequinUser} ({mannequin.Id}) mapped to {targetUser} ({targetUserId})");
            }
            else
            {
                _log.LogInformation($"Failed to map {mannequinUser} ({mannequin.Id}) to {targetUser} ({targetUserId})");
            }

            return reclaimed;
        }

        public async Task<string> GetOrgId(GithubApi github, string githubOrg)
        {
            if (!string.IsNullOrWhiteSpace(githubOrg) && github != null)
            {
                _log.LogInformation($"GITHUB ORG: {githubOrg}");
                var orgId = await github.GetOrganizationId(githubOrg);

                _log.LogInformation($"    Organization Id: {orgId}");

                return orgId;
            }

            throw new ArgumentException("All arguments must be non-null");
        }
    }
}
