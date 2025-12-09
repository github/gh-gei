
- Fixed issue where alert migration commands (migrate-code-scanning-alerts, migrate-secret-alerts) required GH_PAT environment variable even when GitHub tokens were provided via command-line arguments (--github-source-pat, --github-target-pat). These commands now work correctly with CLI-only token authentication.
