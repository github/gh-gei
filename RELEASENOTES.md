- **BREAKING CHANGE:** The `ado2gh integrate-boards` command now uses GitHub App service connections instead of Personal Access Tokens. The `--github-pat` option has been removed and replaced with `--service-connection-id`. This prevents board integrations from breaking when PATs expire. Requires a pre-configured GitHub App service connection in Azure DevOps.
- Fixed `ado2gh integrate-boards` command to properly report errors when GitHub PAT permissions are incorrect, instead of incorrectly reporting success.

