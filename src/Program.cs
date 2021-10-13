using OctoshiftCLI.Commands;
using System.CommandLine;
using System.Threading.Tasks;

namespace OctoshiftCLI
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var root = new RootCommand("Migrates Azure DevOps repos to GitHub");

            root.AddCommand(new GenerateScriptCommand());
            root.AddCommand(new RewirePipelineCommand());
            root.AddCommand(new IntegrateBoardsCommand());
            root.AddCommand(new ShareServiceConnectionCommand());
            root.AddCommand(new DisableRepoCommand());
            root.AddCommand(new ConfigureAutoLinkCommand());
            root.AddCommand(new CreateTeamCommand());
            root.AddCommand(new AddTeamToRepoCommand());
            root.AddCommand(new MigrateRepoCommand());

            await root.InvokeAsync(args);
        }
    }
}
