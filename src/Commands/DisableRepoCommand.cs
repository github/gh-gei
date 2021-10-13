using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace OctoshiftCLI.Commands
{
    public class DisableRepoCommand : Command
    {
        private AdoApi _ado;

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

        private async Task Invoke(string adoOrg, string adoTeamProject, string adoRepo)
        {
            Console.WriteLine("Disabling repo...");
            Console.WriteLine($"ADO ORG: {adoOrg}");
            Console.WriteLine($"ADO TEAM PROJECT: {adoTeamProject}");
            Console.WriteLine($"ADO REPO: {adoRepo}");

            var adoToken = Environment.GetEnvironmentVariable("ADO_PAT");

            if (string.IsNullOrWhiteSpace(adoToken))
            {
                Console.WriteLine("ERROR: NO ADO_PAT FOUND IN ENV VARS, exiting...");
                return;
            }

            _ado = new AdoApi(adoToken);

            var repoId = await _ado.GetRepoId(adoOrg, adoTeamProject, adoRepo);
            await _ado.DisableRepo(adoOrg, adoTeamProject, adoRepo);
        }
    }
}
