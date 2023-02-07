using System;
namespace OctoshiftCLI.Models
{
    public static class ConsoleWriter
    {
        public static async void OutputLogUrl(GithubApi api, string org, string repo, bool wait = true)
        {
            if (api != null && wait)
            {
                var url = await api.GetMigrationLogUrl(org, repo);

                if (string.IsNullOrEmpty(url))
                {
                    Console.WriteLine($"Migration log available at: {url}");
                }
            }
        }
    }
}

