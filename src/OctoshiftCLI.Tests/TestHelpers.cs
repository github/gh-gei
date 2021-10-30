using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace OctoshiftCLI.Tests
{
    public static class TestHelpers
    {
        #region  Constructor, Member variables and misc. helpers
        private static readonly GithubClient _client = new GithubClient(Environment.GetEnvironmentVariable("GH_PAT"));
        private const string TARGET_PREFIX = "OCLI-Int";

        internal static string GetTargetName(string targetType)
        {
            return $"{TARGET_PREFIX}-{targetType}-{DateTime.UtcNow.ToString("yyMMdd-HHmmss")}";
        }

        internal static string TargetOrg
        {
            get => "GuacamoleResearch";
        }

        internal static string SourceOrg
        {
            get => "OCLI";
        }
        #endregion

        #region Command Helpers
        public static void VerifyCommandOption(IReadOnlyList<Option> options, string name, bool required)
        {
            var option = options.Single(x => x.Name == name);

            Assert.Equal(required, option.IsRequired);
        }
        #endregion

        #region REST API Wrappers
        private static async Task<bool> Exists(string url)
        {
            try
            {
                await _client.GetAsync(url);
            }
            catch (Exception ex)
            {
                return (ex.Message.Contains("404")) ? false : throw (ex);
            }

            return true;
        }

        private static async Task<bool> Delete(string url)
        {
            try
            {
                await _client.DeleteAsync(url);
            }
            catch (Exception ex)
            {
                return (ex.Message.Contains("404")) ? false : throw (ex);
            }

            return true;
        }

        internal static async Task<bool> DeleteTeam(string orgName, string teamName)
        {
            return await Delete($"https://api.github.com/orgs/{orgName}/teams/{teamName}");
        }

        internal static async Task<bool> TeamExists(string orgName, string teamName)
        {
            return await Exists($"https://api.github.com/orgs/{orgName}/teams/{teamName}");
        }

        internal static async Task<bool> DeleteRepo(string orgName, string repoName)
        {
            return await Delete($"https://api.github.com/repos/{orgName}/{repoName}");
        }

        internal static async Task<bool> RepoExists(string orgName, string repoName)
        {
            return await Exists($"https://api.github.com/repos/{orgName}/{repoName}");
        }

        #endregion
    }
}