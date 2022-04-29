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

        public ReclaimService(GithubApi githubApi, OctoLogger logger)
        {
            _githubApi = githubApi;
            _log = logger;
        }
        public virtual async Task ReclaimMannequin(string mannequinUser, string targetUser, string githubOrg, bool force)
        {
            var githubOrgId = await _githubApi.GetOrganizationId(githubOrg);

            var mannequin = await _githubApi.GetMannequin(githubOrgId, mannequinUser);

            if (mannequin == null || mannequin.Id == null)
            {
                throw new OctoshiftCliException($"User {mannequinUser} is not a mannequin.");
            }

            if (mannequin.MappedUser != null && force == false)
            {
                throw new OctoshiftCliException($"User {mannequinUser} has been already mapped to {mannequin.MappedUser.Login}. Use the force option if you want to reclaim the mannequin again.");
            }

            var targetUserId = await _githubApi.GetUserId(targetUser);

            if (targetUserId == null)
            {
                throw new OctoshiftCliException($"Target user {targetUser} not found.");
            }

            var result = await _githubApi.ReclaimMannequin(githubOrgId, mannequin.Id, targetUserId);

            ProcessResult(mannequinUser, targetUser, mannequin, targetUserId, result, true);
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
            if (!ReclaimConstants.CSVHEADER.Equals(lines[0], StringComparison.OrdinalIgnoreCase))
            {
                throw new OctoshiftCliException($"Invalid Header. Should be: {ReclaimConstants.CSVHEADER}");
            }

            var githubOrgId = await _githubApi.GetOrganizationId(githubTargetOrg);

            var returnedMannequins = await _githubApi.GetMannequins(githubOrgId);

            var mannequins = new Mannequins(returnedMannequins.ToArray());

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

                ProcessResult(login, claimantLogin, mannequin, claimantId, result, false);
            }
        }

        private void ProcessResult(string mannequinUser, string targetUser, Mannequin mannequin, string targetUserId, MannequinReclaimResult result, bool failOnError)
        {
            if (result.Errors != null)
            {
                ProcessError($"Failed to reclaim {mannequinUser} ({mannequin.Id}) to {targetUser} ({targetUserId}) Reason: {result.Errors[0].Message}", failOnError);
                return;
            }

            if (result.Data.CreateAttributionInvitation is null ||
                result.Data.CreateAttributionInvitation.Source.Id != mannequin.Id ||
                result.Data.CreateAttributionInvitation.Target.Id != targetUserId)
            {
                ProcessError($"Failed to reclaim {mannequinUser} ({mannequin.Id}) to {targetUser} ({targetUserId})", failOnError);
                return;
            }

            _log.LogInformation($"Successfully reclaimed {mannequinUser} ({mannequin.Id}) to {targetUser} ({targetUserId})");
        }

        private void ProcessError(string message, bool failOnError)
        {
            if (failOnError)
            {
                throw new OctoshiftCliException(message);
            }

            _log.LogError(message);
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
