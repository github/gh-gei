using System;
using System.Threading.Tasks;
using OctoshiftCLI.Services;


namespace OctoshiftCLI.Commands.AbortMigration;

public class AbortMigrationCommandHandler : ICommandHandler<AbortMigrationCommandArgs>
{

    private readonly OctoLogger _log;
    private readonly GithubApi _githubApi;

    public AbortMigrationCommandHandler(OctoLogger log, GithubApi githubApi)
    {
        _log = log;
        _githubApi = githubApi;
    }

    public async Task Handle(AbortMigrationCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        await AbortRepositoryMigration(args.MigrationId);
    }

    private async Task AbortRepositoryMigration(string migrationId)
    {
        var success = await _githubApi.AbortMigration(migrationId);

        if (!success)
        {
            _log.LogError($"Failed to abort migration {migrationId}");
            return;
        }

        _log.LogInformation($"Migration {migrationId} was cancelled");
    }
}
