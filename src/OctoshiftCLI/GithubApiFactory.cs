using System;

namespace OctoshiftCLI
{
    public static class GithubApiFactory
    {
        public static Func<GithubApi> Create = () =>
        {
            var githubToken = GetGithubToken();
            var client = new GithubClient(githubToken);

            return new GithubApi(client);
        };

        public static Func<string> GetGithubToken = () =>
        {
            var githubToken = Environment.GetEnvironmentVariable("GH_PAT");

            if (string.IsNullOrWhiteSpace(githubToken))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("ERROR: NO GH_PAT FOUND IN ENV VARS, exiting...");
                Console.ResetColor();
                return null;
            }

            return githubToken;
        };
    }
}