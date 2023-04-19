using System.Diagnostics.Contracts;

namespace OctoshiftCLI.Services;

public static class MigrateRepoErrorHandler
{
    public static OctoshiftCliException DecorateInsufficientPermissionsException(OctoshiftCliException ex, string orgName)
    {
        Contract.Requires(ex != null);
        var errorSuffix = GenerateInsufficientPermissionsHelpMessage(orgName);
        return new OctoshiftCliException($"{ex.Message}. {errorSuffix}", ex);
    }

    private static string GenerateInsufficientPermissionsHelpMessage(string orgName)
    {
        return $"Please check that (a) you are a member of the `{orgName}` organization, (b) you are an organization owner or you have been granted the migrator role and (c) your personal access token has the correct scopes. For more information, see https://docs.github.com/en/migrations/using-github-enterprise-importer/preparing-to-migrate-with-github-enterprise-importer/managing-access-for-github-enterprise-importer.";
    }
}
