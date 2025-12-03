- Added `--migration-id` option to `download-logs` command to allow downloading logs directly by migration ID without requiring org/repo lookup
- Added validation to detect and return clear error messages when a URL is provided instead of a name for organization, repository, or enterprise arguments (e.g., `--github-org`, `--github-target-org`, `--source-repo`, `--github-target-enterprise`)

