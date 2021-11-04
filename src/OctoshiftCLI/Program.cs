using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace OctoshiftCLI
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            //var root = new RootCommand("Migrates Azure DevOps repos to GitHub");

            //root.AddCommand(new GenerateScriptCommand());
            //root.AddCommand(new RewirePipelineCommand());
            //root.AddCommand(new IntegrateBoardsCommand());
            //root.AddCommand(new ShareServiceConnectionCommand());
            //root.AddCommand(new DisableRepoCommand());
            //root.AddCommand(new LockRepoCommand());
            //root.AddCommand(new ConfigureAutoLinkCommand());
            //root.AddCommand(new CreateTeamCommand());
            //root.AddCommand(new AddTeamToRepoCommand());
            //root.AddCommand(new MigrateRepoCommand());
            //root.AddCommand(new GrantMigratorRoleCommand());
            //root.AddCommand(new RevokeMigratorRoleCommand());

            var serviceProvider = new ServiceCollection().AddCommands().BuildServiceProvider();
            var parser = BuildParser(serviceProvider);

            await parser.InvokeAsync(args);





            //var builder = new CommandLineBuilder(root);
            //var host = Host.CreateDefaultBuilder();
            //host.ConfigureServices(services =>
            //{
            //    services.AddSingleton<AdoApi>();
            //    services.AddSingleton(_ => AdoClientFactory.Create());
            //    services.AddSingleton<GenerateScriptCommand>();
            //    services.AddSingleton<RewirePipelineCommand>();
            //    services.AddSingleton<IntegrateBoardsCommand>();
            //    services.AddSingleton<ShareServiceConnectionCommand>();
            //    services.AddSingleton<DisableRepoCommand>();
            //    services.AddSingleton<LockRepoCommand>();
            //    services.AddSingleton<ConfigureAutoLinkCommand>();
            //    services.AddSingleton<CreateTeamCommand>();
            //    services.AddSingleton<AddTeamToRepoCommand>();
            //    services.AddSingleton<MigrateRepoCommand>();
            //    services.AddSingleton<GrantMigratorRoleCommand>();
            //    services.AddSingleton<RevokeMigratorRoleCommand>();
            //});

            //var parser = builder.UseHost(_ => Host.CreateDefaultBuilder(),
            //                               host =>
            //                               {
            //                                   host.ConfigureServices(services =>
            //                                   {
            //                                       services.AddSingleton<AdoApi>();
            //                                       services.AddSingleton(_ => AdoClientFactory.Create());
            //                                       services.AddSingleton<GenerateScriptCommand>();
            //                                       services.AddSingleton<RewirePipelineCommand>();
            //                                       services.AddSingleton<IntegrateBoardsCommand>();
            //                                       services.AddSingleton<ShareServiceConnectionCommand>();
            //                                       services.AddSingleton<DisableRepoCommand>();
            //                                       services.AddSingleton<LockRepoCommand>();
            //                                       services.AddSingleton<ConfigureAutoLinkCommand>();
            //                                       services.AddSingleton<CreateTeamCommand>();
            //                                       services.AddSingleton<AddTeamToRepoCommand>();
            //                                       services.AddSingleton<MigrateRepoCommand>();
            //                                       services.AddSingleton<GrantMigratorRoleCommand>();
            //                                       services.AddSingleton<RevokeMigratorRoleCommand>();
            //                                   });
            //                               })
            //             .UseDefaults()
            //             .Build();

            //await parser.InvokeAsync(args);
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
    }
}