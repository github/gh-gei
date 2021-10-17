using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace OctoshiftCLI.Commands
{
    public class LockRepoCommand : Command
    {
        private AdoApi _ado;

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

        private async Task Invoke(string adoOrg, string adoTeamProject, string adoRepo)
        {
            Console.WriteLine("Locking repo...");
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

            _ado = new AdoApi(adoToken);

            var teamProjectId = await _ado.GetTeamProjectId(adoOrg, adoTeamProject);
            var repoId = await _ado.GetRepoId(adoOrg, adoTeamProject, adoRepo);
            var identityDescriptor = await _ado.GetIdentityDescriptor(adoOrg, teamProjectId, "Project Valid Users");
            await _ado.LockRepo(adoOrg, teamProjectId, repoId, identityDescriptor);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Repo successfully disabled");
            Console.ResetColor();
        }
    }
}
