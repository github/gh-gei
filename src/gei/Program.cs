using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
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

        public static async Task Main(string[] args)
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection
                .AddCommands()
                .AddSingleton(Logger)
                .AddSingleton<EnvironmentVariableProvider>()
                .AddSingleton<GithubApiFactory>()
                .AddSingleton<AzureApiFactory>()
                .AddSingleton<AdoApiFactory>()
                .AddTransient<ITargetGithubApiFactory>(sp => sp.GetRequiredService<GithubApiFactory>())
                .AddTransient<ISourceGithubApiFactory>(sp => sp.GetRequiredService<GithubApiFactory>())
                .AddTransient<IAzureApiFactory>(sp => sp.GetRequiredService<AzureApiFactory>())
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

            await parser.InvokeAsync(args);
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