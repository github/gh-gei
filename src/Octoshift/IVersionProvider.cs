namespace OctoshiftCLI
{
    public interface IVersionProvider
    {
        string GetCurrentVersion();
        string GetVersionComments();
    }
}
