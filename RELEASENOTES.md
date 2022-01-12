# Release Notes

- Renamed the CLI from octoshift to ado2gh to indicate that this one is specifically for Azure DevOps -> GitHub migrations (in the future there will be additional CLI's for other migration scenarios)
- We will automatically remove secrets from log files and console output (previously the verbose logs would contain your PAT's)