using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace OctoshiftCLI.AdoToGithub.Commands
{
    public class ReclaimMannequinCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly GithubApiFactory _githubApiFactory;

        public ReclaimMannequinCommand(OctoLogger log, GithubApiFactory githubApiFactory) : base("reclaim-mannequin")
        {
            _log = log;
            _githubApiFactory = githubApiFactory;

            Description = "Reclaims a mannequin user. An invite will be sent and the user will have to accept for the remapping to occur.";

            var githubOrgOption = new Option<string>("--github-org")
            {
                IsRequired = true,
                Description = "Uses GH_PAT env variable or --github-pat arg."
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
            var githubPatOption = new Option<string>("--github-pat")
            {
                IsRequired = false
            };
            var verbose = new Option("--verbose")
            {
                IsRequired = false
            };

            AddOption(githubOrgOption);
            AddOption(mannequinUsernameOption);
            AddOption(targetUsernameOption);
            AddOption(forceOption);
            AddOption(githubPatOption);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, string, bool, string, bool>(Invoke);
        }

        public async Task Invoke(
          string githubOrg,
          string mannequinUser,
          string targetUser,
          bool force = false,
          string githubPat = null,
          bool verbose = false)
        {
            _log.Verbose = verbose;

            _log.LogInformation("Reclaming Mannequin...");

            _log.LogInformation($"GITHUB ORG: {githubOrg}");
            _log.LogInformation($"MANNEQUIN: {mannequinUser}");
            _log.LogInformation($"RECLAIMING USER: {targetUser}");
            if (githubPat is not null)
            {
                _log.LogInformation("GITHUB PAT: ***");
            }

            var githubApi = _githubApiFactory.Create(githubPat, Name);

            _log.LogInformation($"GITHUB ORG: {githubOrg}");
            var githubOrgId = await githubApi.GetOrganizationId(githubOrg);
            _log.LogInformation($"    Organization Id: {githubOrgId}");

            var mannequin = await githubApi.GetMannequin(githubOrgId, mannequinUser);

            if (mannequin == null || mannequin.Id == null)
            {
                throw new OctoshiftCliException($"User {mannequinUser} is not a mannequin.");
            }

            if (mannequin.MappedUser != null && force == false)
            {
                throw new OctoshiftCliException($"User {mannequinUser} has been already mapped to {mannequin.MappedUser.Login}. Use the force option if you want to reclaim the mannequin again.");
            }

            var targetUserId = await githubApi.GetUserId(targetUser);

            if (targetUserId == null)
            {
                throw new OctoshiftCliException($"Target user {targetUser} not found.");
            }

            var result = await githubApi.ReclaimMannequin(githubOrgId, mannequin.Id, targetUserId);

            if (result.Errors != null)
            {
                throw new OctoshiftCliException($"Failed to reclaim {mannequinUser} ({mannequin.Id}) to {targetUser} ({targetUserId}) Reason: {result.Errors[0].Message}");
            }

            if (result.Data.CreateAttributionInvitation is null ||
                result.Data.CreateAttributionInvitation.Source.Id != mannequin.Id ||
                result.Data.CreateAttributionInvitation.Target.Id != targetUserId)
            {
                throw new OctoshiftCliException($"Failed to reclaim {mannequinUser} ({mannequin.Id}) to {targetUser} ({targetUserId})");
            }

            _log.LogInformation($"Successfully reclaimed {mannequinUser} ({mannequin.Id}) to {targetUser} ({targetUserId})");
        }
    }
}
