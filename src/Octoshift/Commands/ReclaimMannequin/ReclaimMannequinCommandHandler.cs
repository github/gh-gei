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

    internal Func<string, bool> FileExists = path => File.Exists(path);
    internal Func<string, string[]> GetFileContent = path => File.ReadLines(path).ToArray();

    public ReclaimMannequinCommandHandler(OctoLogger log, ReclaimService reclaimService, ConfirmationService confirmationService)
    {
        _log = log;
        _reclaimService = reclaimService;
        _confirmationService = confirmationService;
    }

    public async Task Handle(ReclaimMannequinCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        if (!string.IsNullOrEmpty(args.Csv))
        {
            _log.LogInformation("Reclaiming Mannequins with CSV...");

            if (!FileExists(args.Csv))
            {
                throw new OctoshiftCliException($"File {args.Csv} does not exist.");
            }

            if (args.SkipInvitation && !args.NoPrompt)
            {
                _ = _confirmationService.AskForConfirmation("Reclaiming mannequins with the --skip-invitation option is immediate and irreversible. Are you sure you wish to continue? (y/n)");
            }

            await _reclaimService.ReclaimMannequins(GetFileContent(args.Csv), args.GithubOrg, args.Force, args.SkipInvitation);
        }
        else
        {

            _log.LogInformation("Reclaiming Mannequin...");

            await _reclaimService.ReclaimMannequin(args.MannequinUser, args.MannequinId, args.TargetUser, args.GithubOrg, args.Force);
        }
    }
}
