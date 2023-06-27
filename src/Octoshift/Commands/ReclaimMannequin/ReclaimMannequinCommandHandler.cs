using System;
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

            await _reclaimService.ReclaimMannequins(GetFileContent(args.Csv), args.GithubOrg, args.Force, args.SkipInvitation);
        }
        else
        {

            _log.LogInformation("Reclaiming Mannequin...");

            await _reclaimService.ReclaimMannequin(args.MannequinUser, args.MannequinId, args.TargetUser, args.GithubOrg, args.Force, args.SkipInvitation);
        }
    }
}
