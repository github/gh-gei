using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.GithubEnterpriseImporter.Commands;

namespace OctoshiftCLI.GithubEnterpriseImporter
{
    public static class Program
    {
        private static readonly OctoLogger Logger = new OctoLogger();

        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "If the version check fails for any reason, we want the CLI to carry on with the current command")]
        public static async Task Main(string[] args)
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection
                .AddCommands()
                .AddSingleton(Logger)
                .AddSingleton<EnvironmentVariableProvider>()
                .AddSingleton<GithubApiFactory>()
                .AddSingleton<AdoApiFactory>()
                .AddSingleton<IBlobServiceClientFactory, BlobServiceClientFactory>()
                .AddSingleton<IAzureApiFactory, AzureApiFactory>()
                .AddSingleton<RetryPolicy>()
                .AddSingleton<VersionChecker>()
                .AddSingleton<IVersionProvider, VersionChecker>(sp => sp.GetRequiredService<VersionChecker>())
                .AddTransient<ITargetGithubApiFactory>(sp => sp.GetRequiredService<GithubApiFactory>())
                .AddTransient<ISourceGithubApiFactory>(sp => sp.GetRequiredService<GithubApiFactory>())
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

            try
            {
                await LatestVersionCheck(serviceProvider);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Could not retrieve latest gei CLI version from github.com, please ensure you are using the latest version by running: gh extension upgrade gei");
                Logger.LogVerbose(ex.ToString());
            }

            await parser.InvokeAsync(args);
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
            var root = new RootCommand("CLI for GitHub Enterprise Importer.");
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
