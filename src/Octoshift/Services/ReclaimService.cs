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

    public virtual async Task ReclaimMannequin(string mannequinUser, string mannequinId, string targetUser, string githubOrg, bool force, bool skipInvitation)
    {
        var githubOrgId = await _githubApi.GetOrganizationId(githubOrg);

        var mannequins = new Mannequins((await GetMannequinsByLogin(githubOrgId, mannequinUser)).GetByLogin(mannequinUser, mannequinId));

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

        if (skipInvitation)
        {
            foreach (var mannequin in mannequins.GetUniqueUsers())
            {
                var result = await _githubApi.ReclaimMannequinSkipInvitation(githubOrgId, mannequin.Id, targetUserId);

                // If results return a fail-fast error, we should break out of the for-loop
                if (!HandleReclaimationResult(mannequin.Login, targetUser, mannequin, targetUserId, result))
                {
                    throw new OctoshiftCliException("Failed to reclaim mannequin.");
                }
            }

        }
        else
        {
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

        var mannequins = await GetMannequins(githubOrgId);

        // Parse CSV
        var parsedMannequins = new List<Mannequin>();

        foreach (var line in lines.Skip(1).Where(l => l != null && l.Trim().Length > 0))
        {
            var (login, userid, claimantLogin) = ParseLine(line);

            parsedMannequins.Add(new Mannequin()
            {
                Login = login,
                Id = userid,
                MappedUser = new Claimant()
                {
                    Login = claimantLogin
                }
            });
        }

        // Validate CSV and claim mannequins
        foreach (var mannequin in parsedMannequins)
        {
            if (mannequin.Login == null)
            {
                continue;
            }

            if (!force && mannequins.IsClaimed(mannequin.Login, mannequin.Id))
            {
                _log.LogWarning($"{mannequin.Login} is already claimed. Skipping (use force if you want to reclaim)");
                continue;
            }

            if (mannequins.FindFirst(mannequin.Login, mannequin.Id) == null)
            {
                _log.LogWarning($"Mannequin {mannequin.Login} not found. Skipping.");
                continue;
            }

            if (parsedMannequins.Where(x => x.Login == mannequin.Login && x.Id == mannequin.Id).Count() > 1)
            {
                _log.LogWarning($"Mannequin {mannequin.Login} is a duplicate. Skipping.");
                continue;
            }

            string claimantId;

            try
            {
                claimantId = await _githubApi.GetUserId(mannequin.MappedUser.Login);
            }
            catch (OctoshiftCliException ex) when (ex.Message.Contains("Could not resolve to a User with the login"))
            {
                _log.LogWarning($"Claimant \"{mannequin.MappedUser.Login}\" not found. Will ignore it.");
                continue;
            }

            if (skipInvitation)
            {
                var result = await _githubApi.ReclaimMannequinSkipInvitation(githubOrgId, mannequin.Id, claimantId);

                // If results return a fail-fast error, we should break out of the for-loop
                if (!HandleReclaimationResult(mannequin.Login, mannequin.MappedUser.Login, mannequin, claimantId, result))
                {
                    return;
                }
            }
            else
            {
                var result = await _githubApi.CreateAttributionInvitation(githubOrgId, mannequin.Id, claimantId);
                HandleInvitationResult(mannequin.Login, mannequin.MappedUser.Login, mannequin, claimantId, result);
            }
        }
    }

    private async Task<Mannequins> GetMannequins(string githubOrgId)
    {
        var returnedMannequins = await _githubApi.GetMannequins(githubOrgId);

        return new Mannequins(returnedMannequins);
    }

    private async Task<Mannequins> GetMannequinsByLogin(string githubOrgId, string login)
    {
        var returnedMannequins = await _githubApi.GetMannequinsByLogin(githubOrgId, login);

        return new Mannequins(returnedMannequins);
    }

    private bool HandleInvitationResult(string mannequinUser, string targetUser, Mannequin mannequin, string targetUserId, CreateAttributionInvitationResult result)
    {
        if (result.Errors != null)
        {
            _log.LogError($"Failed to send reclaim invitation email to {targetUser} for mannequin {mannequinUser} ({mannequin.Id}): {result.Errors[0].Message}");
            return false;
        }

        if (result.Data.CreateAttributionInvitation is null ||
            result.Data.CreateAttributionInvitation.Source.Id != mannequin.Id ||
            result.Data.CreateAttributionInvitation.Target.Id != targetUserId)
        {
            _log.LogError($"Failed to send reclaim invitation email to {targetUser} for mannequin {mannequinUser} ({mannequin.Id})");
            return false;
        }

        _log.LogInformation($"Mannequin reclaim invitation email successfully sent to {targetUser} for {mannequinUser} ({mannequin.Id})");

        return true;
    }

    private bool HandleReclaimationResult(string mannequinUser, string targetUser, Mannequin mannequin, string targetUserId, ReattributeMannequinToUserResult result)
    {
        if (result.Errors != null)
        {
            // Writing as switch statement in anticipation of other errors that will need specific logic
            switch (result.Errors[0].Message)
            {
                case string a when a.Contains("is not an Enterprise Managed Users (EMU) organization"):
                    _log.LogError("Failed to reclaim mannequins. The --skip-invitation flag is only available to EMU organizations.");
                    return false; // Indicates we should stop parsing through the CSV
                default:
                    _log.LogWarning($"Failed to reattribute content belonging to mannequin {mannequinUser} ({mannequin.Id}) to {targetUser}: {result.Errors[0].Message}");
                    return true;
            }
        }

        if (result.Data.ReattributeMannequinToUser is null ||
            result.Data.ReattributeMannequinToUser.Source.Id != mannequin.Id ||
            result.Data.ReattributeMannequinToUser.Target.Id != targetUserId)
        {

            _log.LogWarning($"Failed to reattribute content belonging to mannequin {mannequinUser} ({mannequin.Id}) to {targetUser}");
            return true;
        }

        _log.LogInformation($"Successfully reclaimed content belonging to mannequin {mannequinUser} ({mannequin.Id}) to {targetUser}");

        return true; // Indiciates we should continue onto the next mannequin
    }

    private (string MannequinUser, string MannequinId, string TargetUser) ParseLine(string line)
    {
        var components = line.Split(',');

        if (components.Length != 3)
        {
            _log.LogWarning($"Invalid line: \"{line}\". Will ignore it.");
            return (null, null, null);
        }

        var login = components[0].Trim();
        var userId = components[1].Trim();
        var claimantLogin = components[2].Trim();

        if (string.IsNullOrEmpty(login))
        {
            _log.LogWarning($"Invalid line: \"{line}\". Mannequin login is not defined. Will ignore it.");
            return (null, null, null);
        }

        if (string.IsNullOrEmpty(userId))
        {
            _log.LogWarning($"Invalid line: \"{line}\". Mannequin Id is not defined. Will ignore it.");
            return (null, null, null);
        }

        if (string.IsNullOrEmpty(claimantLogin))
        {
            _log.LogWarning($"Invalid line: \"{line}\". Target User is not defined. Will ignore it.");
            return (null, null, null);
        }

        return (login, userId, claimantLogin);
    }
}
