using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands;

public class ArchiveGitHubRepoCommand : Command
{
    private readonly EnvironmentVariableProvider _environmentVariableProvider;
    private readonly OctoLogger _log;
    private readonly ISourceGithubApiFactory _sourceGithubApiFactory;

    public ArchiveGitHubRepoCommand(OctoLogger log, ISourceGithubApiFactory sourceGithubApiFactory,
        EnvironmentVariableProvider environmentVariableProvider) : base("archive-gh-repo")
    {
        _log = log;
        _sourceGithubApiFactory = sourceGithubApiFactory;
        _environmentVariableProvider = environmentVariableProvider;

        Description = "Invokes the GitHub APIs to migrate the repo and all repo data.";

        var githubSourceOrg = new Option<string>("--github-source-org")
        {
            IsRequired = true,
            Description = "Uses GH_SOURCE_PAT env variable or --github-source-pat option. Will fall back to GH_PAT or --github-target-pat if not set."
        };

        var githubSourcePat = new Option<string>("--github-source-pat")
        {
            IsRequired = false,
            Description = "PAT used to connect to the source repo. If not provided, will default to use GH_SOURCE_PAT env variable."
        };

        var sourceRepo = new Option<string>("--source-repo") { IsRequired = true };

        var ghesApiUrl = new Option<string>("--ghes-api-url")
        {
            IsRequired = false,
            Description = "Required if migrating from GHES. The API endpoint for your GHES instance. For example: http(s)://ghes.contoso.com/api/v3"
        };

        var noSslVerify = new Option("--no-ssl-verify")
        {
            IsRequired = false,
            Description =
                "Only effective if migrating from GHES. Disables SSL verification when communicating with your GHES instance. All other migration steps will continue to verify SSL. If your GHES instance has a self-signed SSL certificate then setting this flag will allow data to be extracted."
        };

        var verbose = new Option("--verbose") { IsRequired = false };

        AddOption(githubSourceOrg);
        AddOption(sourceRepo);
        AddOption(githubSourcePat);
        AddOption(ghesApiUrl);
        AddOption(noSslVerify);
        AddOption(verbose);

        Handler = CommandHandler.Create<ArchiveGitHubRepoCommandArgs>(Invoke);
    }

    public async Task Invoke(ArchiveGitHubRepoCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.Verbose = args.Verbose;

        var sourceToken = args.GithubSourcePat ?? _environmentVariableProvider.SourceGithubPersonalAccessToken();

        var githubApi = args.NoSslVerify
            ? _sourceGithubApiFactory.CreateClientNoSsl(args.GhesApiUrl, sourceToken)
            : _sourceGithubApiFactory.Create(args.GhesApiUrl, sourceToken);

        if (args.GhesApiUrl.HasValue())
        {
            _log.LogInformation($"GHES API URL: {args.GhesApiUrl}");
            _log.LogVerbose($"Ignore SSL: {args.NoSslVerify}");
        }

        _log.LogInformation($"Archiving the repository '{args.GithubSourceOrg}/{args.SourceRepo}'");

        await githubApi.ArchiveRepository(args.GithubSourceOrg, args.SourceRepo);
    }
}

public class ArchiveGitHubRepoCommandArgs
{
    public string GhesApiUrl { get; set; }
    public string GithubSourceOrg { get; set; }
    public string GithubSourcePat { get; set; }
    public bool NoSslVerify { get; set; }
    public string SourceRepo { get; set; }
    public bool Verbose { get; set; }
}
