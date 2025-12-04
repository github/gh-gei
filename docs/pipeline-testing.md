# Pipeline Testing Feature

This document explains how to use the new pipeline testing functionality in the `gh ado2gh rewire-pipeline` command.

## Overview

The pipeline testing feature allows you to validate Azure DevOps pipelines before permanently migrating them to GitHub. It provides two testing modes:

1. **Single Pipeline Test (`--dry-run`)**: Test a specific pipeline
2. **Batch Pipeline Test (`test-pipelines` command)**: Test multiple pipelines concurrently

## Single Pipeline Testing

### Usage

```bash
gh ado2gh rewire-pipeline --ado-org "MyOrg" --ado-team-project "MyProject" \
  --ado-pipeline "MyPipeline" --github-org "MyGitHubOrg" --github-repo "MyRepo" \
  --service-connection-id "12345" --dry-run --monitor-timeout-minutes 45
```

### Parameters

- `--dry-run`: Enables test mode (temporarily rewires, tests, then restores)
- `--monitor-timeout-minutes`: How long to wait for build completion (default: 30 minutes)

### Process Flow

1. **Retrieve Pipeline Information**: Gets current pipeline configuration and repository details
2. **Temporary Rewiring**: Points pipeline to GitHub repository temporarily
3. **Build Execution**: Queues a test build and captures the build ID
4. **Restoration**: Automatically restores pipeline to original ADO repository
5. **Build Monitoring**: Monitors build progress until completion or timeout
6. **Report Generation**: Provides detailed test results and recommendations

### Sample Output

```
Starting dry-run mode: Testing pipeline rewiring to GitHub...
Step 1: Retrieving pipeline information...
Pipeline ID: 123
Original ADO Repository: MyAdoRepo
Step 2: Temporarily rewiring pipeline to GitHub...
✓ Pipeline successfully rewired to GitHub
Step 3: Queuing a test build...
Build queued with ID: 456
Build URL: https://dev.azure.com/MyOrg/MyProject/_build/results?buildId=456
Step 4: Restoring pipeline back to original ADO repository...
✓ Pipeline successfully restored to original ADO repository
Step 5: Monitoring build progress (timeout: 30 minutes)...
Build completed with result: succeeded

=== PIPELINE TEST REPORT ===
ADO Organization: MyOrg
ADO Team Project: MyProject
Pipeline Name: MyPipeline
Build Result: succeeded
✅ Pipeline test PASSED - Build completed successfully
```

## Batch Pipeline Testing

### Usage

```bash
gh ado2gh test-pipelines --ado-org "MyOrg" --ado-team-project "MyProject" \
  --github-org "MyGitHubOrg" --github-repo "MyRepo" --service-connection-id "12345" \
  --pipeline-filter "CI-*" --max-concurrent-tests 3 --report-path "test-results.json"
```

### Parameters

- `--pipeline-filter`: Wildcard pattern to filter pipelines (e.g., "CI-*", "*-Deploy", "*")
- `--max-concurrent-tests`: Maximum number of concurrent tests (default: 3)
- `--report-path`: Path to save detailed JSON report (default: pipeline-test-report.json)
- `--monitor-timeout-minutes`: Timeout for each pipeline test (default: 30 minutes)

### Process Flow

1. **Pipeline Discovery**: Finds all pipelines matching the filter criteria
2. **Concurrent Testing**: Runs multiple pipeline tests simultaneously (respecting concurrency limits)
3. **Automatic Restoration**: Ensures all pipelines are restored to ADO
4. **Progress Monitoring**: Tracks each pipeline's test progress independently
5. **Comprehensive Reporting**: Generates both console summary and detailed JSON report

### Sample Output

```
Starting batch pipeline testing...
Step 1: Discovering pipelines...
Found 15 pipelines to test
Step 2: Testing pipelines (max concurrent: 3)...
Testing pipeline: CI-Frontend (ID: 101)
Testing pipeline: CI-Backend (ID: 102)
Testing pipeline: CI-Database (ID: 103)
...

=== PIPELINE BATCH TEST SUMMARY ===
Total Pipelines Tested: 15
Successful Builds: 12
Failed Builds: 2
Timed Out Builds: 1
Rewiring Errors: 0
Restoration Errors: 0
Success Rate: 80.0%
Total Test Time: 01:45:32
```

## Report Structure

### JSON Report Format

```json
{
  "TotalPipelines": 15,
  "SuccessfulBuilds": 12,
  "FailedBuilds": 2,
  "TimedOutBuilds": 1,
  "ErrorsRewiring": 0,
  "ErrorsRestoring": 0,
  "TotalTestTime": "01:45:32",
  "SuccessRate": 80.0,
  "Results": [
    {
      "AdoOrg": "MyOrg",
      "AdoTeamProject": "MyProject",
      "AdoRepoName": "MyAdoRepo",
      "PipelineName": "CI-Frontend",
      "PipelineId": 101,
      "PipelineUrl": "https://dev.azure.com/MyOrg/MyProject/_build/definition?definitionId=101",
      "BuildId": 201,
      "BuildUrl": "https://dev.azure.com/MyOrg/MyProject/_build/results?buildId=201",
      "Status": "completed",
      "Result": "succeeded",
      "StartTime": "2025-01-01T10:00:00Z",
      "EndTime": "2025-01-01T10:15:00Z",
      "BuildDuration": "00:15:00",
      "RewiredSuccessfully": true,
      "RestoredSuccessfully": true,
      "IsSuccessful": true
    }
  ]
}
```

## Error Handling and Recovery

### Automatic Recovery

The testing system is designed to be resilient:

- **Restoration Guarantee**: Pipelines are always restored to ADO, even if tests fail
- **Concurrent Safety**: Multiple tests run safely without interfering with each other
- **Timeout Handling**: Tests that exceed timeout limits are automatically terminated
- **Error Isolation**: One failed test doesn't affect others in batch mode

### Manual Recovery

If automatic restoration fails:

1. Check the console output for "MANUAL RESTORATION REQUIRED" messages
2. Use the provided Pipeline ID and URL to manually restore the pipeline
3. Review the detailed JSON report for specific error information

## Best Practices

### Before Testing

1. **Verify Service Connection**: Ensure the GitHub service connection works properly
2. **Check Repository Access**: Confirm the GitHub repository is accessible from ADO
3. **Review Branch Strategy**: Ensure the default branch exists in both repositories
4. **Test with Small Batch**: Start with a few pipelines before testing all

### During Testing

1. **Monitor Progress**: Watch console output for any immediate issues
2. **Check Build Status**: Use provided build URLs to monitor detailed progress
3. **Resource Management**: Limit concurrent tests based on your ADO capacity

### After Testing

1. **Review Results**: Analyze both successful and failed test results
2. **Address Issues**: Fix identified problems before permanent migration
3. **Document Findings**: Use test results to plan migration strategy
4. **Validate Restoration**: Ensure all pipelines were properly restored

## Troubleshooting

### Common Issues

**Build Failures**
- Check GitHub repository exists and is accessible
- Verify service connection has proper permissions
- Ensure YAML pipeline files are compatible

**Restoration Failures**
- Verify ADO permissions are sufficient
- Check if original repository still exists
- Manual restoration may be required

**Timeout Issues**
- Increase `--monitor-timeout-minutes` for longer builds
- Check if builds are queued but not starting
- Verify agent pools are available

### Support Information

For issues or questions about pipeline testing:
1. Review the detailed error messages in console output
2. Check the JSON report for specific failure details
3. Verify permissions and connectivity to both ADO and GitHub
