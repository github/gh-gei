
- Fixed issue where CLI commands required GH_PAT environment variable even when GitHub tokens were provided via command-line arguments (--github-source-pat, --github-target-pat). All migration commands now work correctly with CLI-only token authentication.
