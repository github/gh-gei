using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace OctoshiftCLI.Commands
{
    public class RewirePipelineCommand : Command
    {
        public RewirePipelineCommand() : base("rewire-pipeline")
        {
            var adoOrg = new Option<string>("--ado-org")
            {
                IsRequired = true
            };
            var adoTeamProject = new Option<string>("--ado-team-project")
            {
                IsRequired = true
            };
            var adoPipeline = new Option<string>("--ado-pipeline")
            {
                IsRequired = true
            };
            var githubOrg = new Option<string>("--github-org")
            {
                IsRequired = true
            };
            var githubRepo = new Option<string>("--github-repo")
            {
                IsRequired = true
            };
            var serviceConnectionId = new Option<string>("--service-connection-id")
            {
                IsRequired = true
            };

            AddOption(adoOrg);
            AddOption(adoTeamProject);
            AddOption(adoPipeline);
            AddOption(githubOrg);
            AddOption(githubRepo);
            AddOption(serviceConnectionId);

            Handler = CommandHandler.Create<string, string, string, string, string, string>(Invoke);
        }

        public async Task Invoke(string adoOrg, string adoTeamProject, string adoPipeline, string githubOrg, string githubRepo, string serviceConnectionId)
        {
            Console.WriteLine($"Rewiring Pipeline to GitHub repo...");
            Console.WriteLine($"ADO ORG: {adoOrg}");
            Console.WriteLine($"ADO TEAM PROJECT: {adoTeamProject}");
            Console.WriteLine($"ADO PIPELINE: {adoPipeline}");
            Console.WriteLine($"GITHUB ORG: {githubOrg}");
            Console.WriteLine($"GITHUB REPO: {githubRepo}");
            Console.WriteLine($"SERVICE CONNECTION ID: {serviceConnectionId}");

            var adoToken = Environment.GetEnvironmentVariable("ADO_PAT");

            if (string.IsNullOrWhiteSpace(adoToken))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("ERROR: NO ADO_PAT FOUND IN ENV VARS, exiting...");
                Console.ResetColor();
                return;
            }

            using var ado = AdoApiFactory.Create(adoToken);

            var adoPipelineId = await ado.GetPipelineId(adoOrg, adoTeamProject, adoPipeline);
            var pipelineDetails = await ado.GetPipeline(adoOrg, adoTeamProject, adoPipelineId);
            await ado.ChangePipelineRepo(pipelineDetails, githubOrg, githubRepo, serviceConnectionId);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Successfully rewired pipeline");
            Console.ResetColor();
        }
    }
}
