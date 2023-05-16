using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Octoshift.Models;
using OctoshiftCLI.Models;

namespace OctoshiftCLI.Services;

public class ReclaimService
{
    private readonly GithubApi _githubApi;
    private readonly OctoLogger _log;

    public const string CSVHEADER = "mannequin-user,mannequin-id,target-user";

    private class Mannequins
    {
        private readonly Mannequin[] _mannequins;

        public Mannequins(IEnumerable<Mannequin> mannequins)
        {
            _mannequins = mannequins.ToArray();
        }

        public Mannequin FindFirst(string login, string userid)
        {
            return _mannequins.FirstOrDefault(m => login.Equals(m.Login, StringComparison.OrdinalIgnoreCase) && userid.Equals(m.Id, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Gets all mannequins by login and (optionally by login and user id)
        /// </summary>
        /// <param name="mannequinUser"></param>
        /// <param name="mannequinId">null to ignore</param>
        /// <returns></returns>
        internal IEnumerable<Mannequin> GetByLogin(string mannequinUser, string mannequinId)
        {
            return _mannequins.Where(
                    m => mannequinUser.Equals(m.Login, StringComparison.OrdinalIgnoreCase) &&
                        (mannequinId == null || mannequinId.Equals(m.Id, StringComparison.OrdinalIgnoreCase))
                );
        }

        /// <summary>
        /// Checks if the user has been claimed at least once (regardless of the last reclaiming result)
        /// </summary>
        /// <param name="login"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public bool IsClaimed(string login, string id)
        {
            return _mannequins.FirstOrDefault(m =>
                login.Equals(m.Login, StringComparison.OrdinalIgnoreCase) &&
                id.Equals(m.Id, StringComparison.OrdinalIgnoreCase)
                && m.MappedUser != null)?.Login != null;
        }

        public bool IsClaimed(string login)
        {
            return _mannequins.FirstOrDefault(m =>
                login.Equals(m.Login, StringComparison.OrdinalIgnoreCase)
                && m.MappedUser != null)?.Login != null;
        }

        public bool IsEmpty()
        {
            return _mannequins.Length == 0;
        }

        public IEnumerable<Mannequin> GetUniqueUsers()
        {
            return _mannequins.DistinctBy(x => $"{x.Id}__{x.Login}");
        }
    }

    public ReclaimService(GithubApi githubApi, OctoLogger logger)
    {
        _githubApi = githubApi;
        _log = logger;
    }

    public virtual async Task ReclaimMannequin(string mannequinUser, string mannequinId, string targetUser, string githubOrg, bool force)
    {
        var githubOrgId = await _githubApi.GetOrganizationId(githubOrg);

        var mannequins = new Mannequins((await GetMannequins(githubOrgId)).GetByLogin(mannequinUser, mannequinId));
        if (mannequins.IsEmpty())
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

        var success = true;

        // get all unique mannequins by login and id and map them all to the same target
        foreach (var mannequin in mannequins.GetUniqueUsers())
        {
            var result = await _githubApi.CreateAttributionInvitation(githubOrgId, mannequin.Id, targetUserId);

            success &= HandleInvitationResult(mannequinUser, targetUser, mannequin, targetUserId, result);
        }

        if (!success)
        {
            throw new OctoshiftCliException("Failed to send reclaim mannequin invitation(s).");
        }
    }

    public virtual async Task ReclaimMannequins(string[] lines, string githubTargetOrg, bool force, bool skipInvitation)
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
        if (!CSVHEADER.Equals(lines[0], StringComparison.OrdinalIgnoreCase))
        {
            throw new OctoshiftCliException($"Invalid Header. Should be: {CSVHEADER}");
        }

        var githubOrgId = await _githubApi.GetOrganizationId(githubTargetOrg);

        // org.enterprise_managed_user_enabled?

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

            string claimantId;

            try
            {
                claimantId = await _githubApi.GetUserId(claimantLogin);
            }
            catch (OctoshiftCliException ex) when (ex.Message.Contains("Could not resolve to a User with the login"))
            {
                _log.LogError($"Claimant \"{claimantLogin}\" not found. Will ignore it.");
                continue;
            }

            if (skipInvitation)
            {
                //TODO: Check if org is emu before continuing, throw error if not
                dynamic result = await _githubApi.ReclaimMannequinsSkipInvitation(githubOrgId, userid, claimantId);
                HandleReclaimationResult(login, claimantLogin, mannequin, claimantId, result);
            }
            else
            {
                dynamic result = await _githubApi.CreateAttributionInvitation(githubOrgId, userid, claimantId);
                HandleInvitationResult(login, claimantLogin, mannequin, claimantId, result);
            }
        }
    }

    private async Task<Mannequins> GetMannequins(string githubOrgId)
    {
        var returnedMannequins = await _githubApi.GetMannequins(githubOrgId);

        return new Mannequins(returnedMannequins);
    }

    private bool HandleInvitationResult(string mannequinUser, string targetUser, Mannequin mannequin, string targetUserId, CreateAttributionInvitationResult result)
    {
        if (result.Errors != null)
        {
            _log.LogError($"Failed to invite {mannequinUser} ({mannequin.Id}) to {targetUser} ({targetUserId}) Reason: {result.Errors[0].Message}");
            return false;
        }

        if (result.Data.CreateAttributionInvitation is null ||
            result.Data.CreateAttributionInvitation.Source.Id != mannequin.Id ||
            result.Data.CreateAttributionInvitation.Target.Id != targetUserId)
        {
            _log.LogError($"Failed to invite {mannequinUser} ({mannequin.Id}) to {targetUser} ({targetUserId})");
            return false;
        }

        _log.LogInformation($"Mannequin reclaim invitation email successfully sent to: {mannequinUser} ({mannequin.Id}) for {targetUser} ({targetUserId})");

        return true;
    }

    private bool HandleReclaimationResult(string mannequinUser, string targetUser, Mannequin mannequin, string targetUserId, ReattributeMannequinToUserResult result)
    {
        if (result.Errors != null)
        {
            _log.LogError($"Failed to reclaim {mannequinUser} ({mannequin.Id}) to {targetUser} ({targetUserId}): {result.Errors[0].Message}");
            return false;
        }

        if (result.Data.ReattributeMannequinToUser is null ||
            result.Data.ReattributeMannequinToUser.Source.Id != mannequin.Id ||
            result.Data.ReattributeMannequinToUser.Target.Id != targetUserId)
        {

            _log.LogError($"Failed to reclaim {mannequinUser} ({mannequin.Id}) to {targetUser} ({targetUserId})");
            return false;
        }

        _log.LogInformation($"Successfully reclaimed {mannequinUser} ({mannequin.Id}) to {targetUser} ({targetUserId})");

        return true;
    }

    private (string MannequinUser, string MannequinId, string TargetUser) ParseLine(string line)
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
