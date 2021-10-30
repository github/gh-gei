using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace OctoshiftCLI.Commands
{
    public class LockRepoCommand : Command
    {
        public LockRepoCommand() : base("lock-ado-repo")
        {
            Description = "Makes the ADO repo read-only for all users. It does this by adding Deny permissions for the Project Valid Users group on the repo.";

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
            var adoToken = Environment.GetEnvironmentVariable("ADO_PAT");

            if (string.IsNullOrWhiteSpace(adoToken))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("ERROR: NO ADO_PAT FOUND IN ENV VARS, exiting...");
                Console.ResetColor();
                return;
            }

            using var ado = AdoApiFactory.Create(adoToken);

            await LockRepo(adoOrg, adoTeamProject, adoRepo, ado);
        }

        private async Task LockRepo(string adoOrg, string adoTeamProject, string adoRepo, AdoApi ado)
        {
            Console.WriteLine("Locking repo...");
            Console.WriteLine($"ADO ORG: {adoOrg}");
            Console.WriteLine($"ADO TEAM PROJECT: {adoTeamProject}");
            Console.WriteLine($"ADO REPO: {adoRepo}");

            var teamProjectId = await ado.GetTeamProjectId(adoOrg, adoTeamProject);
            var repoId = await ado.GetRepoId(adoOrg, adoTeamProject, adoRepo);
            var identityDescriptor = await ado.GetIdentityDescriptor(adoOrg, teamProjectId, "Project Valid Users");
            await ado.LockRepo(adoOrg, teamProjectId, repoId, identityDescriptor);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Repo successfully locked");
            Console.ResetColor();
        }
    }
}