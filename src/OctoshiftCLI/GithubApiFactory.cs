using System;

namespace OctoshiftCLI
{
    public static class GithubApiFactory
    {
        public static Func<string, GithubApi> Create = token => new GithubApi(token);
    }
}
