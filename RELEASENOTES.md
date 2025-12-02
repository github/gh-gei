- Added `--migration-id` option to `download-logs` command to allow downloading logs directly by migration ID without requiring org/repo lookup

- bbs2gh : Added validation for `--archive-path` and `--bbs-shared-home` options to fail fast with clear error messages if the provided paths do not exist or are not accessible. Archive path is now logged before upload operations to help with troubleshooting
