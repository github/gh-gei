- Fixed a cosmetic issue where the migration source for GitHub Enterprise Cloud with data residency (ghe.com) sources showed `https://github.com` instead of the tenant URL. This did not affect migrations.
- When migrating from GitHub Enterprise Cloud with data residency (ghe.com) with `--use-github-storage`, the CLI no longer emits the inapplicable GitHub Enterprise Server (GHES) Management Console warning. Instead it notes that `--use-github-storage` is not required for ghe.com sources unless you are supplying your own on-disk archive paths.

