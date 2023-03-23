- Improve error message when `migrate-repo` is used with a target personal access token (PAT) with insufficient permissions
- Ensure `--no-ssl-verify` flag is honored when downloading archives from GHES
- `--bbs-project` and `--bbs-repo` are now both required in `gh bbs2gh migrate-repo` command when `--bbs-server-url` is set
* Added `--keep-archive` flag to `gh gei migrate-repo` and `gh gei generate-script`. When migrating from GHES this will skip the step where we delete the archive from your machine, leaving it around as a local file.
- Continue to next mannequin mapping in `gh gei reclaim-mannequin --csv` if a username doesn't exist
- Introduce a new command `gh gei migrate-code-scanning-alerts` which migrates all code-scanning analysis and alert states for the default branch. This is useful if you want to migrate the history of code-scanning alerts together with their current state (open, reopened, fixed). For dismissed alerts, the dismissed-reason (e.g. won't fix, false positive etc) and dismissed-comment will also be migrated to the target repo. 
