using System.CommandLine;
using System.CommandLine.Invocation;

namespace OctoshiftCLI.Commands;

public class ShareServiceConnectionCommand : Command
{
    private readonly OctoLogger _log;
    private readonly AdoApiFactory _adoFactory;

    public ShareServiceConnectionCommand(OctoLogger log, AdoApiFactory adoFactory) : base("share-service-connection")
    {
        _log = log;
        _adoFactory = adoFactory;

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
        var verbose = new Option("--verbose")
        {
            IsRequired = false
        };

        AddOption(adoOrg);
        AddOption(adoTeamProject);
        AddOption(serviceConnectionId);
        AddOption(verbose);

        Handler = CommandHandler.Create<string, string, string, bool>(Invoke);
    }

    public async Task Invoke(string adoOrg, string adoTeamProject, string serviceConnectionId, bool verbose = false)
    {
        _log.Verbose = verbose;

        _log.LogInformation("Sharing Service Connection...");
        _log.LogInformation($"ADO ORG: {adoOrg}");
        _log.LogInformation($"ADO TEAM PROJECT: {adoTeamProject}");
        _log.LogInformation($"SERVICE CONNECTION ID: {serviceConnectionId}");

        using var ado = _adoFactory.Create();

        var adoTeamProjectId = await ado.GetTeamProjectId(adoOrg, adoTeamProject);
        // TODO: If the service connection is already shared with this team project this will crash
        await ado.ShareServiceConnection(adoOrg, adoTeamProject, adoTeamProjectId, serviceConnectionId);

        _log.LogSuccess("Successfully shared service connection");
    }
}