using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace OctoshiftCLI.Commands
{
    public class ShareServiceConnectionCommand : Command
    {
        private AdoApi _ado;

        public ShareServiceConnectionCommand() : base("share-service-connection")
        {
            var adoOrg = new Option<string>("--ado-org")
            {
                IsRequired = true
            };
            var adoTeamProject = new Option<string>("--ado-team-project")
            {
                IsRequired = true
            };
            var serviceConnectionId = new Option<string>("--service-connection-id")
            {
                IsRequired = true
            };

            AddOption(adoOrg);
            AddOption(adoTeamProject);
            AddOption(serviceConnectionId);

            Handler = CommandHandler.Create<string, string, string>(Invoke);
        }

        private async Task Invoke(string adoOrg, string adoTeamProject, string serviceConnectionId)
        {
            Console.WriteLine("Sharing Service Connection...");
            Console.WriteLine($"ADO ORG: {adoOrg}");
            Console.WriteLine($"ADO TEAM PROJECT: {adoTeamProject}");
            Console.WriteLine($"SERVICE CONNECTION ID: {serviceConnectionId}");

            var adoToken = Environment.GetEnvironmentVariable("ADO_PAT");

            if (string.IsNullOrWhiteSpace(adoToken))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("ERROR: NO ADO_PAT FOUND IN ENV VARS, exiting...");
                Console.ResetColor();
                return;
            }

            _ado = new AdoApi(adoToken);

            // TODO: If the service connection is already shared with this team project this will crash
            var adoTeamProjectId = await _ado.GetTeamProjectId(adoOrg, adoTeamProject);
            await _ado.ShareServiceConnection(adoOrg, adoTeamProject, adoTeamProjectId, serviceConnectionId);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Successfully shared service connection");
            Console.ResetColor();
        }
    }
}
