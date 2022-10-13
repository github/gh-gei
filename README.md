# GitHub Enterprise Importer CLI

[![Actions Status: CI](https://github.com/github/gh-gei/workflows/CI/badge.svg)](https://github.com/github/gh-gei/actions?query=workflow%3ACI)


The [GitHub Enterprise Importer](https://docs.github.com/en/early-access/github/migrating-with-github-enterprise-importer) (GEI, formerly Octoshift) is a highly customizable API-first migration offering designed to help you move your enterprise to GitHub Enterprise Cloud. The GEI-CLI wraps the GEI APIs as a cross-platform console application to simplify customizing your migration experience.

> GEI is in a public beta for GitHub Enterprise Cloud.

## Using the GEI CLI
There are 2 separate CLIs that we ship as extensions for the official [GitHub CLI](https://github.com/cli/cli#installation):
- `gh gei` - Run migrations between GitHub products
- `gh ado2gh` - Run migrations from Azure DevOps to GitHub

To use `gh gei` first install the latest [GitHub CLI](https://github.com/cli/cli#installation), then run the command
>`gh extension install github/gh-gei`

To use `gh ado2gh` first install the latest [GitHub CLI](https://github.com/cli/cli#installation), then run the command
>`gh extension install github/gh-ado2gh`

We update the extensions frequently, so make sure you update them on a regular basis:
>`gh extension upgrade github/gh-gei`

To see the available commands and options run:

>`gh gei --help`

>`gh ado2gh --help`

### GitHub to GitHub Usage (GitHub.com -> GitHub.com)
1. Create Personal Access Tokens with access to the source GitHub org, and the target GitHub org (for more details on scopes needed refer to our [official documentation](https://docs.github.com/en/early-access/github/migrating-with-github-enterprise-importer)).

2. Set the GH_SOURCE_PAT and GH_PAT environment variables.

3. Run the `generate-script` command to generate a migration PowerShell script.
>`gh gei generate-script --github-source-org ORGNAME --github-target-org ORGNAME`

4. The previous command will have created a `migrate.ps1` script. Review the steps in the generated script and tweak if necessary.

5. The migrate.ps1 script requires PowerShell to run. If not already installed see the [install instructions](https://docs.microsoft.com/en-us/powershell/scripting/install/installing-powershell?view=powershell-7.2) to install PowerShell on Windows, Linux, or Mac. Then run the script.

Refer to the [official documentation](https://docs.github.com/en/early-access/github/migrating-with-github-enterprise-importer) for more details, including differences when migrating from GitHub Enterprise Server.

### Azure DevOps to GitHub Usage
1. Create Personal Access Tokens with access to the Azure DevOps org, and the GitHub org (for more details on scopes needed refer to our [official documentation](https://docs.github.com/en/early-access/github/migrating-with-github-enterprise-importer)).

2. Set the `ADO_PAT` and `GH_PAT` environment variables.

3. Run the `generate-script` command to generate a migration script.
>`gh ado2gh generate-script --ado-org ORGNAME --github-org ORGNAME --all`

4. The previous command will have created a `migrate.ps1` PowerShell script. Review the steps in the generated script and tweak if necessary.

5. The `migrate.ps1` script requires PowerShell to run. If not already installed see the [install instructions](https://docs.microsoft.com/en-us/powershell/scripting/install/installing-powershell?view=powershell-7.2) to install PowerShell on Windows, Linux, or Mac. Then run the script.

Refer to the [official documentation](https://docs.github.com/en/early-access/github/migrating-with-github-enterprise-importer) for more details.

## Quick Start Videos
You'll find videos below to help you quickly get started with the GEI CLI. Be sure to pick the videos relevant to your migration scenario. 

>NOTE: We don't update these videos as often as we update the CLI, so they may not exactly match the functionality in the latest release of this CLI.

### Migrating from Azure DevOps to GitHub
Video guides below will help you get started with your first migration. Then help you build up to orchestrating a complete end-to-end production migration. 
* Running your first few migrations: https://www.youtube.com/watch?v=yfnXbwtXY80
* Orchestrating an end-to-end production migration: https://www.youtube.com/watch?v=AtFB-U1Og4c

## Contributions

See [Contributing](CONTRIBUTING.md) for more info on how to get involved.
