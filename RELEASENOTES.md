
- Adds the ability to migrate ADO repos using the `gh gei` CLI. This overlaps with some of the capabilities of ado2gh, but the `gh gei` will not include all the extra ADO migration capabilities like re-wiring pipelines and boards integration.
    - `gh gei generate-script` now has an `--ado-source-org` option
    - `gh gei migrate-repo` now has `--ado-source-org` and `--ado-team-project` options
- Added `grant-migrator-role` and `revoke-migrator-role` commands to `gh gei`
- Add gei command path for generating a migration archive `gh gei generate-archive` which uses the migration api on that instance to generate two archives of data, the metadata for a repository and the git data for that repository