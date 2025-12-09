using OctoshiftCLI.Services;

namespace OctoshiftCLI.Contracts;

public interface ISourceGithubApiFactory
{
    GithubApi Create(string apiUrl = null, string uploadsUrl = null, string sourcePersonalAccessToken = null);
    GithubApi CreateClientNoSsl(string apiUrl = null, string uploadsUrl = null, string sourcePersonalAccessToken = null);
}
