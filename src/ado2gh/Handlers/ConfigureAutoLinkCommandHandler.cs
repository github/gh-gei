using System;
using System.Linq;
using System.Threading.Tasks;
using OctoshiftCLI.AdoToGithub.Commands;
using OctoshiftCLI.Handlers;

namespace OctoshiftCLI.AdoToGithub.Handlers;

public class ConfigureAutoLinkCommandHandler : ICommandHandler<ConfigureAutoLinkCommandArgs>
{
    private readonly OctoLogger _log;
    private readonly GithubApi _githubApi;

    public ConfigureAutoLinkCommandHandler(OctoLogger log, GithubApi githubApi)
    {
        _log = log;
        _githubApi = githubApi;
    }

    public async Task Handle(ConfigureAutoLinkCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.Verbose = args.Verbose;

        _log.LogInformation("Configuring Autolink Reference...");
        _log.LogInformation($"GITHUB ORG: {args.GithubOrg}");
        _log.LogInformation($"GITHUB REPO: {args.GithubRepo}");
        _log.LogInformation($"ADO ORG: {args.AdoOrg}");
        _log.LogInformation($"ADO TEAM PROJECT: {args.AdoTeamProject}");
        if (args.GithubPat is not null)
        {
            _log.LogInformation("GITHUB PAT: ***");
        }

        _log.RegisterSecret(args.GithubPat);

        var keyPrefix = "AB#";
        var urlTemplate = $"https://dev.azure.com/{args.AdoOrg}/{args.AdoTeamProject}/_workitems/edit/<num>/";

        var autoLinks = await _githubApi.GetAutoLinks(args.GithubOrg, args.GithubRepo);
        if (autoLinks.Any(al => al.KeyPrefix == keyPrefix && al.UrlTemplate == urlTemplate))
        {
            _log.LogSuccess($"Autolink reference already exists for key_prefix: '{keyPrefix}'. No operation will be performed");
            return;
        }

        var autoLink = autoLinks.FirstOrDefault(al => al.KeyPrefix == keyPrefix);
        if (autoLink != default((int, string, string)))
        {
            _log.LogInformation($"Autolink reference already exists for key_prefix: '{keyPrefix}', but the url template is incorrect");
            _log.LogInformation($"Deleting existing Autolink reference for key_prefix: '{keyPrefix}' before creating a new Autolink reference");
            await _githubApi.DeleteAutoLink(args.GithubOrg, args.GithubRepo, autoLink.Id);
        }

        await _githubApi.AddAutoLink(args.GithubOrg, args.GithubRepo, keyPrefix, urlTemplate);

        _log.LogSuccess("Successfully configured autolink references");
    }
}
