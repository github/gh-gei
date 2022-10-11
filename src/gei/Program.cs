using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.GithubEnterpriseImporter
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
                .AddSingleton<GithubApiFactory>()
                .AddSingleton<AdoApiFactory>()
                .AddSingleton<IBlobServiceClientFactory, BlobServiceClientFactory>()
                .AddSingleton<IAzureApiFactory, AzureApiFactory>()
                .AddSingleton<AwsApiFactory>()
                .AddSingleton<RetryPolicy>()
                .AddSingleton<VersionChecker>()
                .AddSingleton<HttpDownloadService>()
                .AddSingleton<DateTimeProvider>()
                .AddSingleton<IVersionProvider, VersionChecker>(sp => sp.GetRequiredService<VersionChecker>())
                .AddSingleton<ITargetGithubApiFactory>(sp => sp.GetRequiredService<GithubApiFactory>())
                .AddSingleton<ISourceGithubApiFactory>(sp => sp.GetRequiredService<GithubApiFactory>())
                .AddHttpClient("NoSSL")
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
                {
                    AllowAutoRedirect = false,
                    CheckCertificateRevocationList = false,
                    ServerCertificateCustomValidationCallback = delegate { return true; }
                })
                .Services
                .AddHttpClient("Default")
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
                {
                    AllowAutoRedirect = false
                });

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var parser = BuildParser(serviceProvider);

            SetContext(parser.Parse(args));

            try
            {
                await LatestVersionCheck(serviceProvider);
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Could not retrieve latest gei CLI version from github.com, please ensure you are using the latest version by running: gh extension upgrade gei");
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
                Logger.LogInformation($"You are running the latest version of the gei CLI [v{await versionChecker.GetLatestVersion()}]");
            }
            else
            {
                Logger.LogWarning($"You are running an older version of the gei CLI [v{versionChecker.GetCurrentVersion()}]. The latest version is v{await versionChecker.GetLatestVersion()}.");
                Logger.LogWarning($"Please update by running: gh extension upgrade gei");
            }
        }

        private static Parser BuildParser(ServiceProvider serviceProvider)
        {
            var root = new RootCommand("CLI for GitHub Enterprise Importer.")
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
