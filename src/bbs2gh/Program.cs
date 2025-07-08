using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

using OctoshiftCLI.BbsToGithub.Factories;
using OctoshiftCLI.Contracts;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Factories;
using OctoshiftCLI.Services;

[assembly: InternalsVisibleTo("OctoshiftCLI.Tests")]
namespace OctoshiftCLI.BbsToGithub
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
                .AddSingleton<BbsApiFactory>()
                .AddSingleton<ITargetGithubApiFactory, GithubApiFactory>()
                .AddSingleton<RetryPolicy>()
                .AddSingleton<IAzureApiFactory, AzureApiFactory>()
                .AddSingleton<IBlobServiceClientFactory, BlobServiceClientFactory>()
                .AddSingleton<AwsApiFactory>()
                .AddSingleton<BasicHttpClient>()
                .AddSingleton<GithubStatusApi>()
                .AddSingleton<VersionChecker>()
                .AddSingleton<HttpDownloadServiceFactory>()
                .AddSingleton<ProjectsCsvGeneratorService>()
                .AddSingleton<ReposCsvGeneratorService>()
                .AddSingleton<BbsInspectorService>()
                .AddSingleton<BbsInspectorServiceFactory>()
                .AddSingleton<FileSystemProvider>()
                .AddSingleton<DateTimeProvider>()
                .AddSingleton<WarningsCountLogger>()
                .AddSingleton<IVersionProvider, VersionChecker>(sp => sp.GetRequiredService<VersionChecker>())
                .AddSingleton<BbsArchiveDownloaderFactory>()
                .AddSingleton<ConfirmationService>()
                .AddHttpClient("Kerberos", kerberos: true, noSsl: false)
                .AddHttpClient("NoSSL", kerberos: false, noSsl: true)
                .AddHttpClient("KerberosNoSSL", kerberos: true, noSsl: true)
                .AddHttpClient("Default");

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var rootCommand = new RootCommand("Automate end-to-end Bitbucket Server to GitHub migrations.")
                .AddCommands(serviceProvider);

            var commandLineBuilder = new CommandLineBuilder(rootCommand);
            var parser = commandLineBuilder
                .UseDefaults()
                .UseExceptionHandler((ex, _) =>
                {
                    Logger.LogError(ex);
                    Environment.ExitCode = 1;
                }, 1)
                .Build();

            SetContext(new InvocationContext(parser.Parse(args)));

            try
            {
                await GithubStatusCheck(serviceProvider);
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Could not check GitHub availability from githubstatus.com.  See https://www.githubstatus.com for details.");
                Logger.LogVerbose(ex.ToString());
            }

            try
            {
                await LatestVersionCheck(serviceProvider);
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Could not retrieve latest bbs2gh extension version from github.com, please ensure you are using the latest version by running: gh extension upgrade bbs2gh");
                Logger.LogVerbose(ex.ToString());
            }

            await parser.InvokeAsync(args);
        }

        private static void SetContext(InvocationContext context)
        {
            CliContext.RootCommand = "bbs2gh";
            CliContext.ExecutingCommand = context.ParseResult.CommandResult.Command.Name;
        }

        private static async Task GithubStatusCheck(ServiceProvider sp)
        {
            var envProvider = sp.GetRequiredService<EnvironmentVariableProvider>();

            if (envProvider.SkipStatusCheck()?.ToUpperInvariant() is "TRUE" or "1")
            {
                Logger.LogInformation("Skipped GitHub status check due to GEI_SKIP_STATUS_CHECK environment variable");
                return;
            }

            var githubStatusApi = sp.GetRequiredService<GithubStatusApi>();

            if (await githubStatusApi.GetUnresolvedIncidentsCount() > 0)
            {
                Logger.LogWarning("GitHub is currently experiencing availability issues.  See https://www.githubstatus.com for details.");
            }
        }

        private static async Task LatestVersionCheck(ServiceProvider sp)
        {
            var envProvider = sp.GetRequiredService<EnvironmentVariableProvider>();

            if (envProvider.SkipVersionCheck()?.ToUpperInvariant() is "TRUE" or "1")
            {
                Logger.LogInformation("Skipped latest version check due to GEI_SKIP_VERSION_CHECK environment variable");
                return;
            }

            var versionChecker = sp.GetRequiredService<VersionChecker>();

            if (await versionChecker.IsLatest())
            {
                Logger.LogInformation($"You are running an up-to-date version of the bbs2gh extension [v{versionChecker.GetCurrentVersion()}]");
            }
            else
            {
                Logger.LogWarning($"You are running an old version of the bbs2gh extension [v{versionChecker.GetCurrentVersion()}]. The latest version is v{await versionChecker.GetLatestVersion()}.");
                Logger.LogWarning($"Please update by running: gh extension upgrade bbs2gh");
            }
        }

        private static IServiceCollection AddHttpClient(this IServiceCollection serviceCollection, string name, bool kerberos, bool noSsl) => serviceCollection
            .AddHttpClient(name)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                UseDefaultCredentials = kerberos,
                ServerCertificateCustomValidationCallback = noSsl ? delegate { return true; } : null
            })
            .Services;
    }
}
