# Release Notes

- Renamed the CLI from octoshift to ado2gh to indicate that this one is specifically for Azure DevOps -> GitHub migrations (in the future there will be additional CLI's for other migration scenarios)
- Released an extension for the official GitHub CLI that adds support for GitHub -> GitHub migrations (GHEC only for now). To install run: `gh extension install github/gh-gei`. To use run: `gh gei --help`
- Automatically remove secrets from log files and console output (previously the verbose logs would contain your PAT's)
- Added --ssh option to generate-script and migrate-repo commands (in both ado2gh and gh). This forces the migration to use an older version of the API's that uses SSH to push the repos into GitHub. The newer API's use HTTPS instead. However some customers have been running into problems with some repos that work fine using the older SSH API's. In the future this option will be deprecated once the issues with the HTTPS-based API's are resolved.