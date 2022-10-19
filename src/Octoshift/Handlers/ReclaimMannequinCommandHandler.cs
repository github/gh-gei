using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Octoshift;
using OctoshiftCLI.Commands;

namespace OctoshiftCLI.Handlers;

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

        _log.Verbose = args.Verbose;

        _log.RegisterSecret(args.GithubPat);

        if (string.IsNullOrEmpty(args.Csv) && (string.IsNullOrEmpty(args.MannequinUser) || string.IsNullOrEmpty(args.TargetUser)))
        {
            throw new OctoshiftCliException($"Either --csv or --mannequin-user and --target-user must be specified");
        }

        if (!string.IsNullOrEmpty(args.Csv))
        {
            _log.LogInformation("Reclaiming Mannequins with CSV...");

            _log.LogInformation($"GITHUB ORG: {args.GithubOrg}");
            _log.LogInformation($"FILE: {args.Csv}");
            if (args.Force)
            {
                _log.LogInformation("MAPPING RECLAIMED");
            }

            if (!FileExists(args.Csv))
            {
                throw new OctoshiftCliException($"File {args.Csv} does not exist.");
            }

            await _reclaimService.ReclaimMannequins(GetFileContent(args.Csv), args.GithubOrg, args.Force);
        }
        else
        {
            _log.LogInformation("Reclaiming Mannequin...");

            _log.LogInformation($"GITHUB ORG: {args.GithubOrg}");
            _log.LogInformation($"MANNEQUIN: {args.MannequinUser}");
            if (args.MannequinId != null)
            {
                _log.LogInformation($"MANNEQUIN ID: {args.MannequinId}");
            }
            _log.LogInformation($"RECLAIMING USER: {args.TargetUser}");
            if (args.GithubPat is not null)
            {
                _log.LogInformation($"GITHUB PAT: ***");
            }

            await _reclaimService.ReclaimMannequin(args.MannequinUser, args.MannequinId, args.TargetUser, args.GithubOrg, args.Force);
        }
    }
}
