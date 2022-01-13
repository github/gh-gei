using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.gei.Commands;

namespace OctoshiftCLI.gei
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection
                .AddSingleton<Command, MigrateRepoCommand>(sp =>
                {
                    return new MigrateRepoCommand(
                        sp.GetRequiredService<OctoLogger>(),
                        new Lazy<GithubApi>(() => CreateGithubApi(sp, sp.GetRequiredService<EnvironmentVariableProvider>().TargetGithubPersonalAccessToken())),
                        sp.GetRequiredService<EnvironmentVariableProvider>());
                })
                .AddSingleton<Command, GenerateScriptCommand>(sp =>
                {
                    return new GenerateScriptCommand(
                        sp.GetRequiredService<OctoLogger>(),
                        new Lazy<GithubApi>(() => CreateGithubApi(sp, sp.GetRequiredService<EnvironmentVariableProvider>().SourceGitHubPersonalAccessToken())));
                })
                .AddSingleton<OctoLogger>()
                .AddSingleton<EnvironmentVariableProvider>()
                .AddHttpClient("GithubClient", client =>
                {
                    client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
                    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("OctoshiftCLI", "0.1"));
                    client.DefaultRequestHeaders.Add("GraphQL-Features", "import_api");
                });

            var serviceProvider = serviceCollection.BuildServiceProvider();

            var parser = BuildParser(serviceProvider);

            await parser.InvokeAsync(args);
        }

        private static Parser BuildParser(ServiceProvider serviceProvider)
        {
            var root = new RootCommand("Automate end-to-end Github to GitHub migrations.");
            var commandLineBuilder = new CommandLineBuilder(root);

            foreach (var command in serviceProvider.GetServices<Command>())
            {
                commandLineBuilder.AddCommand(command);
            }

            return commandLineBuilder.UseDefaults().Build();
        }

        private static GithubApi CreateGithubApi(IServiceProvider sp, string githubPat)
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("GithubClient");
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", githubPat);

            var octoLogger = sp.GetRequiredService<OctoLogger>();
            var githubClient = new GithubClient(octoLogger, httpClient);
            return new GithubApi(githubClient);
        }
    }
}