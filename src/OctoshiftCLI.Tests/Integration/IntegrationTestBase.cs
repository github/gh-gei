using System;
using System.Diagnostics;
using OctoshiftCLI;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace OctoshiftCLI.Tests.Integration
{
    public abstract class IntegrationTestBase
    {
        protected GithubClient _client;
        public IntegrationTestBase()
        {
            var githubToken = Environment.GetEnvironmentVariable("GH_PAT");
            _client = new GithubClient(githubToken);
        }


        protected abstract string GitHubApiUrl();

        protected async Task<bool> Exists(string orgName, string name)
        {
            try
            {
                var url = GitHubApiUrl().Replace("{orgName}",orgName).Replace("{name}",name);
                var repo = await _client.GetAsync(url);
            } 
            catch (Exception ex)
            {
                return (ex.Message.Contains("404")) ? false : throw(ex);
            } 

            return true;
        }
        
        protected async Task<bool> Delete(string orgName, string name)
        {
            try
            {
                var url = GitHubApiUrl().Replace("{orgName}",orgName).Replace("{name}",name);
                await _client.DeleteAsync(url);
            } 
            catch (Exception ex)
            {
                return (ex.Message.Contains("404")) ? false : throw(ex);
            } 

            return true;
        }
    }
}
