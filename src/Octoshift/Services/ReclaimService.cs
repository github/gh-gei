using System;
using System.Linq;
using System.Threading.Tasks;
using Octoshift.Models;
using OctoshiftCLI;
using OctoshiftCLI.Models;

namespace Octoshift.Services
{
    public class ReclaimService
    {
        private readonly GithubApi _githubApi;
        private readonly OctoLogger _log;
        private bool _failed;

        public const string CSVHEADER = "mannequin-user,mannequin-id,target-user";

        public ReclaimService(GithubApi githubApi, OctoLogger logger)
        {
            _githubApi = githubApi;
            _log = logger;
        }
        public virtual async Task ReclaimMannequin(string mannequinUser, string mannequinId, string targetUser, string githubOrg, bool force)
        {
            var githubOrgId = await _githubApi.GetOrganizationId(githubOrg);

            var mannequins = new Mannequins((await GetMannequins(githubOrgId)).GetByLogin(mannequinUser, mannequinId));
            if (mannequins.Empty())
            {
                throw new OctoshiftCliException($"User {mannequinUser} is not a mannequin.");
            }

            // (Potentially) Save one call to the API to get the targer user
            // // (it also makes it unnecessary to check for claimed users during reclaiming loop)
            if (!force && mannequins.IsClaimed(mannequinUser))
            {
                throw new OctoshiftCliException($"User {mannequinUser} is already mapped to a user. Use the force option if you want to reclaim the mannequin again.");
            }

            var targetUserId = await _githubApi.GetUserId(targetUser);
            if (targetUserId == null)
            {
                throw new OctoshiftCliException($"Target user {targetUser} not found.");
            }

            _failed = false;

            // get all unique mannequins by login and id and map them all to the same target
            foreach (var mannequin in mannequins.UniqueUsers())
            {
                var result = await _githubApi.ReclaimMannequin(githubOrgId, mannequin.Id, targetUserId);

                HandleResult(mannequinUser, targetUser, mannequin, targetUserId, result);
            }

            // Fail if there was at least one error
            HandleResult();
        }

        public virtual async Task ReclaimMannequins(string[] lines, string githubTargetOrg, bool force)
        {
            if (lines == null)
            {
                throw new ArgumentNullException(nameof(lines));
            }

            if (lines.Length == 0)
            {
                _log.LogWarning("File is empty. Nothing to reclaim");
                return;
            }

            // Validate Header
            if (!ReclaimService.CSVHEADER.Equals(lines[0], StringComparison.OrdinalIgnoreCase))
            {
                throw new OctoshiftCliException($"Invalid Header. Should be: {ReclaimService.CSVHEADER}");
            }

            var githubOrgId = await _githubApi.GetOrganizationId(githubTargetOrg);

            var mannequins = await GetMannequins(githubOrgId);

            foreach (var line in lines.Skip(1).Where(l => l != null && l.Trim().Length > 0))
            {
                var (login, userid, claimantLogin) = ParseLine(line);

                if (login == null)
                {
                    continue;
                }

                if (!force && mannequins.IsClaimed(login, userid))
                {
                    _log.LogError($"{login} is already claimed. Skipping (use force if you want to reclaim)");
                    continue;
                }

                var mannequin = mannequins.FindFirst(login, userid);

                if (mannequin == null)
                {
                    _log.LogError($"Mannequin {login} not found. Skipping.");
                    continue;
                }

                var claimantId = await _githubApi.GetUserId(claimantLogin);

                if (claimantId == null)
                {
                    _log.LogError($"Claimant \"{claimantLogin}\" not found. Will ignore it.");
                    continue;
                }

                var result = await _githubApi.ReclaimMannequin(githubOrgId, userid, claimantId);

                HandleResult(login, claimantLogin, mannequin, claimantId, result);
            }
        }

        private async Task<Mannequins> GetMannequins(string githubOrgId)
        {
            var returnedMannequins = await _githubApi.GetMannequins(githubOrgId);

            return new Mannequins(returnedMannequins);
        }

        private void HandleResult(string mannequinUser, string targetUser, Mannequin mannequin, string targetUserId, MannequinReclaimResult result)
        {
            if (result.Errors != null)
            {
                _log.LogError($"Failed to reclaim {mannequinUser} ({mannequin.Id}) to {targetUser} ({targetUserId}) Reason: {result.Errors[0].Message}");
                _failed = true;
                return;
            }

            if (result.Data.CreateAttributionInvitation is null ||
                result.Data.CreateAttributionInvitation.Source.Id != mannequin.Id ||
                result.Data.CreateAttributionInvitation.Target.Id != targetUserId)
            {
                _log.LogError($"Failed to reclaim {mannequinUser} ({mannequin.Id}) to {targetUser} ({targetUserId})");
                _failed = true;
                return;
            }

            _log.LogInformation($"Successfully reclaimed {mannequinUser} ({mannequin.Id}) to {targetUser} ({targetUserId})");
        }

        private void HandleResult()
        {
            if (_failed)
            {
                throw new OctoshiftCliException("Failed to reclaim mannequin(s).");
            }
        }

        private (string, string, string) ParseLine(string line)
        {
            var components = line.Split(',');

            if (components.Length != 3)
            {
                _log.LogError($"Invalid line: \"{line}\". Will ignore it.");
                return (null, null, null);
            }

            var login = components[0].Trim();
            var userId = components[1].Trim();
            var claimantLogin = components[2].Trim();

            if (string.IsNullOrEmpty(login))
            {
                _log.LogError($"Invalid line: \"{line}\". Mannequin login is not defined. Will ignore it.");
                return (null, null, null);
            }

            if (string.IsNullOrEmpty(userId))
            {
                _log.LogError($"Invalid line: \"{line}\". Mannequin Id is not defined. Will ignore it.");
                return (null, null, null);
            }

            if (string.IsNullOrEmpty(claimantLogin))
            {
                _log.LogError($"Invalid line: \"{line}\". Target User is not defined. Will ignore it.");
                return (null, null, null);
            }

            return (login, userId, claimantLogin);
        }
    }
}
