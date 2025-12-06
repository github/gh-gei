using OctoshiftCLI.Services;

namespace OctoshiftCLI.Contracts;

public interface ITargetGithubApiFactory
{
    GithubApi Create(string apiUrl = null, string uploadsUrl = null, string targetPersonalAccessToken = null);
}
