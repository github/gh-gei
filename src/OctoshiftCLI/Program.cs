using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.Commands;

namespace OctoshiftCLI
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection
                .AddCommands()
                .AddSingleton<OctoLogger>()
                .AddSingleton<EnvironmentVariableProvider>()
                .AddSingleton<GithubApi>()
                .AddSingleton<AdoApi>()
                .AddHttpClient<GithubClient>((sp, client) =>
                {
                    client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
                    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("OctoshiftCLI", "0.1"));
                    client.DefaultRequestHeaders.Add("GraphQL-Features", "import_api");
                    var githubToken = sp.GetRequiredService<EnvironmentVariableProvider>().GithubPersonalAccessToken();
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", githubToken);
                });

            serviceCollection.AddHttpClient<AdoClient>((sp, client) =>
                {
                    client.DefaultRequestHeaders.Add("accept", "application/json");

                    var adoToken = sp.GetRequiredService<EnvironmentVariableProvider>().AdoPersonalAccessToken();
                    var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes(string.Format("{0}:{1}", "", adoToken)));
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);
                });

            var serviceProvider = serviceCollection.BuildServiceProvider();

            var parser = BuildParser(serviceProvider);

            await parser.InvokeAsync(args);
        }

        private static Parser BuildParser(ServiceProvider serviceProvider)
        {
            var root = new RootCommand("Migrates Azure DevOps repos to GitHub");
            var commandLineBuilder = new CommandLineBuilder(root);

            foreach (var command in serviceProvider.GetServices<Command>())
            {
                commandLineBuilder.AddCommand(command);
            }

            return commandLineBuilder.UseDefaults().Build();
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