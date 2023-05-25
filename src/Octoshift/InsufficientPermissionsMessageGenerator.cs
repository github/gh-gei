namespace OctoshiftCLI;
public static class InsufficientPermissionsMessageGenerator
{
    public static string Generate(string organizationLogin)
    {
        return $". Please check that:\n  (a) you are a member of the `{organizationLogin}` organization,\n  (b) you are an organization owner or you have been granted the migrator role and\n  (c) your personal access token has the correct scopes.\nFor more information, see https://docs.github.com/en/migrations/using-github-enterprise-importer/preparing-to-migrate-with-github-enterprise-importer/managing-access-for-github-enterprise-importer.";
    }
}
