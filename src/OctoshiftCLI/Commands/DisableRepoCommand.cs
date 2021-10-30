using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace OctoshiftCLI.Commands
{
    public class DisableRepoCommand : Command
    {
        public DisableRepoCommand() : base("disable-ado-repo")
        {
            var adoOrg = new Option<string>("--ado-org")
            {
                IsRequired = true
            };
            var adoTeamProject = new Option<string>("--ado-team-project")
            {
                IsRequired = true
            };
            var adoRepo = new Option<string>("--ado-repo")
            {
                IsRequired = true
            };

            AddOption(adoOrg);
            AddOption(adoTeamProject);
            AddOption(adoRepo);

            Handler = CommandHandler.Create<string, string, string>(Invoke);
        }

        public async Task Invoke(string adoOrg, string adoTeamProject, string adoRepo)
        {
            Console.WriteLine("Disabling repo...");
            Console.WriteLine($"ADO ORG: {adoOrg}");
            Console.WriteLine($"ADO TEAM PROJECT: {adoTeamProject}");
            Console.WriteLine($"ADO REPO: {adoRepo}");

            var adoToken = Environment.GetEnvironmentVariable("ADO_PAT");

            if (string.IsNullOrWhiteSpace(adoToken))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("ERROR: NO ADO_PAT FOUND IN ENV VARS, exiting...");
                Console.ResetColor();
                return;
            }

            using var ado = AdoApiFactory.Create(adoToken);

            var repoId = await ado.GetRepoId(adoOrg, adoTeamProject, adoRepo);
            await ado.DisableRepo(adoOrg, adoTeamProject, repoId);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Repo successfully disabled");
            Console.ResetColor();
        }
    }
}