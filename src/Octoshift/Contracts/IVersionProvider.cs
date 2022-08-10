namespace OctoshiftCLI.Contracts;

public interface IVersionProvider
{
    string GetCurrentVersion();
    string GetVersionComments();
}
