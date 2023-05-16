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

    internal Func<string, bool> FileExists = path => File.Exists(path);
    internal Func<string, string[]> GetFileContent = path => File.ReadLines(path).ToArray();

    public ReclaimMannequinCommandHandler(OctoLogger log, ReclaimService reclaimService)
    {
        _log = log;
        _reclaimService = reclaimService;
    }

    public async Task Handle(ReclaimMannequinCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        if (string.IsNullOrEmpty(args.Csv) && (string.IsNullOrEmpty(args.MannequinUser) || string.IsNullOrEmpty(args.TargetUser)))
        {
            throw new OctoshiftCliException($"Either --csv or --mannequin-user and --target-user must be specified");
        }

        if (!string.IsNullOrEmpty(args.Csv))
        {
            _log.LogInformation("Reclaiming Mannequins with CSV...");

            if (!FileExists(args.Csv))
            {
                throw new OctoshiftCliException($"File {args.Csv} does not exist.");
            }

            await _reclaimService.ReclaimMannequins(GetFileContent(args.Csv), args.GithubOrg, args.Force);
        }
        else
        {
            _log.LogInformation("Reclaiming Mannequin...");

            await _reclaimService.ReclaimMannequin(args.MannequinUser, args.MannequinId, args.TargetUser, args.GithubOrg, args.Force);
        }
    }
}
