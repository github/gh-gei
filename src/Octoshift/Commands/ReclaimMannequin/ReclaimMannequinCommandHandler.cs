using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.Commands.ReclaimMannequin;

public class ReclaimMannequinCommandHandler : ICommandHandler<ReclaimMannequinCommandArgs>
{
    private readonly OctoLogger _log;
    private readonly ReclaimService _reclaimService;
    private readonly ConfirmationService _confirmationService;
    private readonly GithubApi _githubApi;

    internal Func<string, bool> FileExists = path => File.Exists(path);
    internal Func<string, string[]> GetFileContent = path => File.ReadLines(path).ToArray();

    public ReclaimMannequinCommandHandler(OctoLogger log, ReclaimService reclaimService, ConfirmationService confirmationService, GithubApi githubApi)
    {
        _log = log;
        _reclaimService = reclaimService;
        _confirmationService = confirmationService;
        _githubApi = githubApi;
    }

    public async Task Handle(ReclaimMannequinCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        if (args.SkipInvitation)
        {
            // Check if user is admin to EMU org
            var login = await _githubApi.GetLoginName();

            var membership = await _githubApi.GetOrgMembershipForUser(args.GithubOrg, login);

            if (membership != "admin")
            {
                throw new OctoshiftCliException($"User {login} is not an org admin and is not eligible to reclaim mannequins with the --skip-invitation feature.");
            }

            if (!args.NoPrompt)
            {
                _confirmationService.AskForConfirmation("Reclaiming mannequins with the --skip-invitation option is immediate and irreversible. Are you sure you wish to continue? [y/N]");
            }
        }

        if (!string.IsNullOrEmpty(args.Csv))
        {
            _log.LogInformation("Reclaiming Mannequins with CSV...");

            if (!FileExists(args.Csv))
            {
                throw new OctoshiftCliException($"File {args.Csv} does not exist.");
            }

            var lines = GetFileContent(args.Csv);

            ConfirmBotReclaims(args, ParseReclaimTargets(lines));

            await _reclaimService.ReclaimMannequins(lines, args.GithubOrg, args.Force, args.SkipInvitation);
        }
        else
        {

            _log.LogInformation("Reclaiming Mannequin...");

            ConfirmBotReclaims(args, new[] { (args.MannequinUser, args.TargetUser) });

            await _reclaimService.ReclaimMannequin(args.MannequinUser, args.MannequinId, args.TargetUser, args.GithubOrg, args.Force, args.SkipInvitation);
        }
    }

    // Reattributing content to a bot auto-accepts and cannot be undone, so we confirm before proceeding.
    // The source mannequin's login is our only hint that it represents a bot; a non-"[bot]" source is
    // very likely a mis-target (a human's content going to a bot), but the convention is GitHub-specific,
    // so we warn and let the admin proceed rather than blocking.
    private void ConfirmBotReclaims(ReclaimMannequinCommandArgs args, IReadOnlyList<(string MannequinUser, string TargetUser)> reclaims)
    {
        var botReclaims = reclaims
            .Where(r => ReclaimService.IsBotLogin(r.TargetUser))
            .ToList();

        if (botReclaims.Count == 0)
        {
            return;
        }

        foreach (var source in botReclaims
                     .Where(r => !ReclaimService.IsBotLogin(r.MannequinUser))
                     .Select(r => r.MannequinUser)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            _log.LogWarning($"\"{source}\" does not look like a bot mannequin (its login does not end in \"[bot]\"). Are you sure you want to do this?");
        }

        if (args.NoPrompt)
        {
            return;
        }

        var humanCount = reclaims.Count - botReclaims.Count;
        var summary = reclaims.Count > 1
            ? $"You are about to reattribute {botReclaims.Count} mannequin(s) to GitHub App / bot account(s)" +
              (humanCount > 0 ? $" and {humanCount} mannequin(s) to user(s)" : string.Empty) + "."
            : $"You are about to reattribute mannequin \"{botReclaims[0].MannequinUser}\" to the GitHub App / bot account \"{botReclaims[0].TargetUser}\".";

        _confirmationService.AskForConfirmation($"{summary} Reattributing content to a bot is immediate and cannot be undone. Are you sure you wish to continue? [y/N]");
    }

    private static (string MannequinUser, string TargetUser)[] ParseReclaimTargets(string[] lines)
    {
        if (lines == null || lines.Length == 0)
        {
            return Array.Empty<(string, string)>();
        }

        return lines
            .Skip(1) // header
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => l.Split(','))
            .Where(c => c.Length == 3)
            .Select(c => (c[0].Trim(), c[2].Trim()))
            .ToArray();
    }
}
