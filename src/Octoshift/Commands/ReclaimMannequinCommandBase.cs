using System;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Octoshift;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.Commands;

public class ReclaimMannequinCommandBase : Command
{
    private readonly OctoLogger _log;
    private readonly ITargetGithubApiFactory _githubApiFactory;
    private ReclaimService _reclaimService;

    internal Func<string, bool> FileExists = path => File.Exists(path);
    internal Func<string, string[]> GetFileContent = path => File.ReadLines(path).ToArray();

    public ReclaimMannequinCommandBase(OctoLogger log, ITargetGithubApiFactory githubApiFactory, ReclaimService reclaimService = null) : base("reclaim-mannequin")
    {
        _log = log;
        _githubApiFactory = githubApiFactory;
        _reclaimService = reclaimService;

        Description = "Reclaims one or more mannequin user(s). An invite will be sent and the user(s) will have to accept for the remapping to occur."
          + "You can reclaim a single user by using --mannequin-user and --target-user or reclaim mannequins in bulk by using the --csv parameter"
          + Environment.NewLine
          + "The CSV file should contain a column with the user's login name (source) and reclaiming user login (target)."
          + Environment.NewLine
          + "The first line is considered the header and is ignored."
          + Environment.NewLine
          + "If both options are specified The CSV file takes precedence and other options will be ignored";
    }

    protected virtual Option<string> GithubOrg { get; } = new("--github-org")
    {
        IsRequired = true,
        Description = "Uses GH_PAT env variable or --github-pat arg."
    };

    protected virtual Option<string> Csv { get; } = new("--csv")
    {
        IsRequired = false,
        Description = "CSV file path with list of mannequins to be reclaimed."
    };

    protected virtual Option<string> MannequinUsername { get; } = new("--mannequin-user")
    {
        IsRequired = false,
        Description = "The login of the mannequin to be remapped."
    };

    protected virtual Option<string> MannequinId { get; } = new("--mannequin-id")
    {
        IsRequired = false,
        Description = "The Id of the mannequin, in case there are multiple mannequins with the same login you can specify the id to reclaim one of the mannequins."
    };

    protected virtual Option<string> TargetUsername { get; } = new("--target-user")
    {
        IsRequired = false,
        Description = "The login of the target user to be mapped."
    };

    protected virtual Option<bool> Force { get; } = new("--force")
    {
        IsRequired = false,
        Description = "Map the user even if it was previously mapped"
    };

    protected virtual Option<string> GithubPat { get; } = new("--github-pat")
    {
        IsRequired = false
    };

    protected virtual Option<bool> Verbose { get; } = new("--verbose")
    {
        IsRequired = false
    };

    protected void AddOptions()
    {
        AddOption(GithubOrg);
        AddOption(Csv);
        AddOption(MannequinUsername);
        AddOption(MannequinId);
        AddOption(TargetUsername);
        AddOption(Force);
        AddOption(GithubPat);
        AddOption(Verbose);
    }

    public async Task Handle(
      string githubOrg,
      string csv,
      string mannequinUser,
      string mannequinId,
      string targetUser,
      bool force = false,
      string githubPat = null,
      bool verbose = false)
    {
        _log.Verbose = verbose;

        if (string.IsNullOrEmpty(csv) && (string.IsNullOrEmpty(mannequinUser) || string.IsNullOrEmpty(targetUser)))
        {
            throw new OctoshiftCliException($"Either --{Csv.ArgumentHelpName} or --{MannequinUsername.ArgumentHelpName} and --{TargetUsername.ArgumentHelpName} must be specified");
        }

        var githubApi = _githubApiFactory.Create(targetPersonalAccessToken: githubPat);
        _reclaimService ??= new ReclaimService(githubApi, _log);

        if (!string.IsNullOrEmpty(csv))
        {
            _log.LogInformation("Reclaiming Mannequins with CSV...");

            _log.LogInformation($"{GithubOrg.GetLogFriendlyName()}: {githubOrg}");
            _log.LogInformation($"FILE: {csv}");
            if (force)
            {
                _log.LogInformation("MAPPING RECLAIMED");
            }

            if (!FileExists(csv))
            {
                throw new OctoshiftCliException($"File {csv} does not exist.");
            }

            await _reclaimService.ReclaimMannequins(GetFileContent(csv), githubOrg, force);
        }
        else
        {
            _log.LogInformation("Reclaiming Mannequin...");

            _log.LogInformation($"{GithubOrg.GetLogFriendlyName()}: {githubOrg}");
            _log.LogInformation($"MANNEQUIN: {mannequinUser}");
            if (mannequinId != null)
            {
                _log.LogInformation($"{MannequinId.GetLogFriendlyName()}: {mannequinId}");
            }
            _log.LogInformation($"RECLAIMING USER: {targetUser}");
            if (githubPat is not null)
            {
                _log.LogInformation($"{GithubPat.GetLogFriendlyName()}: ***");
            }

            await _reclaimService.ReclaimMannequin(mannequinUser, mannequinId, targetUser, githubOrg, force);
        }
    }
}
