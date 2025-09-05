using System;
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.AdoToGithub.Factories;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.AdoToGithub.Commands.TestPipelines
{
    public class TestPipelinesCommand : CommandBase<TestPipelinesCommandArgs, TestPipelinesCommandHandler>
    {
        public TestPipelinesCommand() : base(
            name: "test-pipelines",
            description: "Tests multiple Azure Pipelines by temporarily rewiring them to GitHub, running builds, and generating a comprehensive report." +
                         Environment.NewLine +
                         "Note: Expects ADO_PAT env variable or --ado-pat option to be set.")
        {
            AddOption(AdoOrg);
            AddOption(AdoTeamProject);
            AddOption(GithubOrg);
            AddOption(GithubRepo);
            AddOption(ServiceConnectionId);
            AddOption(AdoPat);
            AddOption(Verbose);
            AddOption(TargetApiUrl);
            AddOption(MonitorTimeoutMinutes);
            AddOption(PipelineFilter);
            AddOption(MaxConcurrentTests);
            AddOption(ReportPath);
        }

        public Option<string> AdoOrg { get; } = new("--ado-org")
        {
            IsRequired = true
        };
        public Option<string> AdoTeamProject { get; } = new("--ado-team-project")
        {
            IsRequired = true
        };
        public Option<string> GithubOrg { get; } = new("--github-org")
        {
            IsRequired = true
        };
        public Option<string> GithubRepo { get; } = new("--github-repo")
        {
            IsRequired = true
        };
        public Option<string> ServiceConnectionId { get; } = new("--service-connection-id")
        {
            IsRequired = true
        };
        public Option<string> AdoPat { get; } = new("--ado-pat");
        public Option<bool> Verbose { get; } = new("--verbose");
        public Option<string> TargetApiUrl { get; } = new("--target-api-url")
        {
            Description = "The URL of the target API, if not migrating to github.com. Defaults to https://api.github.com"
        };
        public Option<int> MonitorTimeoutMinutes { get; } = new("--monitor-timeout-minutes")
        {
            Description = "Timeout in minutes for monitoring build completion. Defaults to 30 minutes."
        };
        public Option<string> PipelineFilter { get; } = new("--pipeline-filter")
        {
            Description = "Filter pattern for pipeline names (supports wildcards). If not specified, tests all pipelines in the project."
        };
        public Option<int> MaxConcurrentTests { get; } = new("--max-concurrent-tests")
        {
            Description = "Maximum number of pipeline tests to run concurrently. Defaults to 3."
        };
        public Option<string> ReportPath { get; } = new("--report-path")
        {
            Description = "Path to save the detailed test report. Defaults to pipeline-test-report.json"
        };

        public override TestPipelinesCommandHandler BuildHandler(TestPipelinesCommandArgs args, IServiceProvider sp)
        {
            if (args is null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            if (sp is null)
            {
                throw new ArgumentNullException(nameof(sp));
            }

            var log = sp.GetRequiredService<OctoLogger>();
            var adoApiFactory = sp.GetRequiredService<AdoApiFactory>();
            var adoApi = adoApiFactory.Create(args.AdoPat);

            return new TestPipelinesCommandHandler(log, adoApi);
        }
    }
}
