- Added validation to detect and return clear error messages when a URL is provided instead of a name for organization, repository, or enterprise arguments (e.g., `--github-org`, `--github-target-org`, `--source-repo`, `--github-target-enterprise`)

- bbs2gh : Added validation for `--archive-path` and `--bbs-shared-home` options to fail fast with clear error messages if the provided paths do not exist or are not accessible. Archive path is now logged before upload operations to help with troubleshooting
