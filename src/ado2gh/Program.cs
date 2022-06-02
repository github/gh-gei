using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.AdoToGithub.Commands;

[assembly: InternalsVisibleTo("OctoshiftCLI.Tests")]
namespace OctoshiftCLI.AdoToGithub
{
    public static class Program
    {
        private static readonly OctoLogger Logger = new();

        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "If the version check fails for any reason, we want the CLI to carry on with the current command")]
        public static async Task Main(string[] args)
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection
                .AddCommands()
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
                .AddSingleton<IVersionProvider, VersionChecker>(sp => sp.GetRequiredService<VersionChecker>())
                .AddHttpClient();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var parser = BuildParser(serviceProvider);

            SetContext(parser.Parse(args));

            try
            {
                await LatestVersionCheck(serviceProvider);
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Could not retrieve latest ado2gh version from github.com, please ensure you are using the latest version by downloading it from https://github.com/github/gh-gei/releases/latest");
                Logger.LogVerbose(ex.ToString());
            }

            await parser.InvokeAsync(args);
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
                Logger.LogWarning($"Please download the latest version from https://github.com/github/gh-gei/releases/latest");
            }
        }

        private static Parser BuildParser(ServiceProvider serviceProvider)
        {
            var root = new RootCommand("Automate end-to-end Azure DevOps Repos to GitHub migrations.");
            var commandLineBuilder = new CommandLineBuilder(root);

            foreach (var command in serviceProvider.GetServices<Command>())
            {
                commandLineBuilder.AddCommand(command);
            }

            return commandLineBuilder
                .UseDefaults()
                .UseExceptionHandler((ex, _) =>
                {
                    Logger.LogError(ex);
                    Environment.ExitCode = 1;
                }, 1)
                .Build();
        }

        private static IServiceCollection AddCommands(this IServiceCollection services)
        {
            var sampleCommandType = typeof(GenerateScriptCommand);
            var commandType = typeof(Command);

            var commands = sampleCommandType
                .Assembly
                .GetExportedTypes()
                .Where(x => x.Namespace == sampleCommandType.Namespace && commandType.IsAssignableFrom(x));

            foreach (var command in commands)
            {
                services.AddSingleton(commandType, command);
            }

            return services;
        }
    }
}
