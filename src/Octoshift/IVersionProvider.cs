namespace OctoshiftCLI
{
    public interface IVersionProvider
    {
        string GetCurrentVersion();
        string GetProductVersionHeaderValue(string commandName);
    }
}
