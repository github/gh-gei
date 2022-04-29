# GitHub Enterprise Importer CLI

[![Actions Status: CI](https://github.com/github/octoshiftcli/workflows/CI/badge.svg)](https://github.com/github/octoshiftcli/actions?query=workflow%3ACI)


The [GitHub Enterprise Importer](https://docs.github.com/en/early-access/github/migrating-with-github-enterprise-importer) (GEI, formerly Octoshift) is a highly customizable API-first migration offering designed to help you move your enterprise to GitHub Enterprise Cloud. The GEI-CLI wraps the GEI APIs as a cross-platform console application to simplify customizing your migration experience.

> GEI is in a private preview for GitHub Enterprise Cloud. It needs to be enabled before using this CLI. Please reach out to [GitHub Sales](https://github.com/enterprise/contact) to enquire about getting into the private beta. 

## Supported Scenarios
GEI-CLI is continuing to expand what it can support. However, it supports the following scenarios at present:

* Azure DevOps -> GitHub Enterprise Cloud migrations
* GitHub Enterprise Cloud -> GitHub Enterprise Cloud migrations
* GitHub Enterprise Server (version 3.4.1 or newer) -> GitHub Enterprise Cloud migrations

Learn more about what exactly is migrated and any limitations in the [GEI documentation](https://docs.github.com/en/early-access/github/migrating-with-github-enterprise-importer/about-github-enterprise-importer). 

## Using the GEI CLI
There are 2 separate CLI's that we ship:
- `ado2gh` - Intended for migrations from Azure DevOps -> GitHub
- `gh gei` - Intended for GitHub -> GitHub migrations

To use `ado2gh` download the latest version from the [Releases](https://github.com/github/gh-gei/releases/latest) in this repo.

To use `gh gei` first install the latest [GitHub CLI](https://github.com/cli/cli#installation), then run the command
>`gh extension install github/gh-gei`

We update the gei extension frequently, to ensure you're using the latest version run this command on a regular basis:
>`gh extension upgrade github/gh-gei`

To see the available commands and options run:

>`ado2gh --help`

>`gh gei --help`

### GitHub to GitHub Usage (GHEC -> GHEC)
1. Create Personal Access Tokens with access to the source GitHub org, and the target GitHub org (for more details on scopes needed refer to our [official documentation](https://docs.github.com/en/early-access/github/migrating-with-github-enterprise-importer)).

2. Set the GH_SOURCE_PAT and GH_PAT environment variables.

3. Run the `generate-script` command to generate a migration script.
>`gh gei generate-script --github-source-org ORGNAME --github-target-org ORGNAME`

4. The previous command will have created a migrate.ps1 script. Review the steps in the generated script and tweak if necessary.

5. The migrate.ps1 script requires powershell to run. If not already installed see the [install instructions](https://docs.microsoft.com/en-us/powershell/scripting/install/installing-powershell?view=powershell-7.2) to install powershell on Windows, Linux, or Mac. Then run the script.

Refer to the [official documentation](https://docs.github.com/en/early-access/github/migrating-with-github-enterprise-importer) for more details (and differences when migrating from GHES or to GHAE).

### Azure DevOps to GitHub Usage
1. Create Personal Access Tokens with access to the Azure DevOps org, and the GitHub org (for more details on scopes needed refer to our [official documentation](https://docs.github.com/en/early-access/github/migrating-with-github-enterprise-importer)).

2. Set the ADO_PAT and GH_PAT environment variables.

3. Run the `generate-script` command to generate a migration script.
>`ado2gh generate-script --ado-org ORGNAME --github-org ORGNAME --all`

4. The previous command will have created a migrate.ps1 script. Review the steps in the generated script and tweak if necessary.

5. The migrate.ps1 script requires powershell to run. If not already installed see the [install instructions](https://docs.microsoft.com/en-us/powershell/scripting/install/installing-powershell?view=powershell-7.2) to install powershell on Windows, Linux, or Mac. Then run the script.

Refer to the [official documentation](https://docs.github.com/en/early-access/github/migrating-with-github-enterprise-importer) for more details.

## Quick Start Videos
You'll find videos below to help you quickly get started with the GEI CLI. Be sure to pick the videos relevant to your migration scenario. 

>NOTE: We don't update these videos as often as we update the CLI, so they may not exactly match the functionality in the latest release of this CLI.

### Migrating GitHub to GitHub
The quick start video below will help you start migrating between GitHub organizations. 

* Migrating GitHub to GitHub with the GEI CLI: https://youtu.be/5cQkM_8n5YY

### Migrating from Azure DevOps to GitHub
Video guides below will help you get started with your first migration. Then help you build up to orchestrating a complete end-to-end production migration. 
* Running your first few migrations: https://www.youtube.com/watch?v=yfnXbwtXY80
* Orchestrating an end-to-end production migration: https://www.youtube.com/watch?v=AtFB-U1Og4c

## Contributions

See [Contributing](CONTRIBUTING.md) for more info on how to get involved.
