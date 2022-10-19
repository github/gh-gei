using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;

[assembly: InternalsVisibleTo("OctoshiftCLI.Tests")]
namespace OctoshiftCLI.AdoToGithub
{
    public static class Program
    {
        private static readonly OctoLogger Logger = new();

        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "If the version check fails for any reason, we want the CLI to carry on with the current command")]
        public static async Task Main(string[] args)
        {
            Logger.LogDebug("Execution Started");

            var serviceCollection = new ServiceCollection();
            serviceCollection
                .AddSingleton(Logger)
                .AddSingleton<EnvironmentVariableProvider>()
                .AddSingleton<AdoApiFactory>()
                .AddSingleton<GithubApiFactory>()
                .AddSingleton<RetryPolicy>()
                .AddSingleton<VersionChecker>()
                .AddSingleton<HttpDownloadService>()
                .AddSingleton<OrgsCsvGeneratorService>()
                .AddSingleton<TeamProjectsCsvGeneratorService>()
                .AddSingleton<ReposCsvGeneratorService>()
                .AddSingleton<PipelinesCsvGeneratorService>()
                .AddSingleton<AdoInspectorService>()
                .AddSingleton<AdoInspectorServiceFactory>()
                .AddSingleton<DateTimeProvider>()
                .AddSingleton<IVersionProvider, VersionChecker>(sp => sp.GetRequiredService<VersionChecker>())
                .AddTransient<ITargetGithubApiFactory>(sp => sp.GetRequiredService<GithubApiFactory>())
                .AddHttpClient();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var parser = BuildParser(serviceProvider);

            SetContext(parser.Parse(args));

            WarnIfNotUsingExtension();

            try
            {
                await LatestVersionCheck(serviceProvider);
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Could not retrieve latest ado2gh CLI version from github.com, please ensure you are using the latest version by running: gh extension upgrade ado2gh");
                Logger.LogVerbose(ex.ToString());
            }

            await parser.InvokeAsync(args);
        }

        private static void WarnIfNotUsingExtension()
        {
            if (!Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory).EndsWith(Path.Combine("extensions", "gh-ado2gh").ToString()))
            {
                Logger.LogWarning("You are not running the ado2gh CLI as a gh extension. This is not recommended, please run: gh extension install github/gh-ado2gh");
            }
        }

        private static void SetContext(ParseResult parseResult)
        {
            CliContext.RootCommand = parseResult.RootCommandResult.Command.Name;
            CliContext.ExecutingCommand = parseResult.CommandResult.Command.Name;
        }

        private static async Task LatestVersionCheck(ServiceProvider sp)
        {
            var versionChecker = sp.GetRequiredService<VersionChecker>();

            if (await versionChecker.IsLatest())
            {
                Logger.LogInformation($"You are running the latest version of the ado2gh CLI [v{await versionChecker.GetLatestVersion()}]");
            }
            else
            {
                Logger.LogWarning($"You are running an older version of the ado2gh CLI [v{versionChecker.GetCurrentVersion()}]. The latest version is v{await versionChecker.GetLatestVersion()}.");
                Logger.LogWarning($"Please update by running: gh extension upgrade ado2gh");
            }
        }

        private static Parser BuildParser(ServiceProvider serviceProvider)
        {
            var root = new RootCommand("Automate end-to-end Azure DevOps Repos to GitHub migrations.")
                .AddCommands(serviceProvider);
            var commandLineBuilder = new CommandLineBuilder(root);

            return commandLineBuilder
                .UseDefaults()
                .UseExceptionHandler((ex, _) =>
                {
                    Logger.LogError(ex);
                    Environment.ExitCode = 1;
                }, 1)
                .Build();
        }
    }
}
