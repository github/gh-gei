# GitHub Enterprise Importer CLI

[![Actions Status: CI](https://github.com/github/octoshiftcli/workflows/CI/badge.svg)](https://github.com/github/octoshiftcli/actions?query=workflow%3ACI)


The [GitHub Enterprise Importer](https://docs.github.com/en/early-access/github/migrating-with-github-enterprise-importer) (GEI, formerly Octoshift) is a highly customizable API-first migration offering designed to help you move your enterprise to GitHub Enterprise Cloud. The GEI-CLI wraps the GEI APIs to simplify customizing your migration experience. 

> GEI is in a private preview for GitHub Enterprise Cloud. It needs to be enabled before using this CLI. Please reach out to [GitHub Sales](https://github.com/enterprise/contact) to enquire about getting into the private beta. 

This version of the GEI-CLI is informally maintained by GitHub. However, this is **not a supported GitHub product**. Customers leveraging these tools must understand that any support must come through a paid GitHub Expert Services engagement.

## Supported Scenarios
GEI-CLI is continuing to expand what it can support. However, it supports the following scenarios at present:

* Azure DevOps -> GitHub Enterprise Cloud migrations
* GitHub Enterprise Cloud -> GitHub Enterprise Cloud migrations

Learn more about what exactly is migrated and any limitations in the [GEI documentation](https://docs.github.com/en/early-access/github/migrating-with-github-enterprise-importer/about-github-enterprise-importer). 

## Quick Start Videos
You'll find videos below to help you quickly get started with the GEI CLI. Be sure to pick the videos relevant to your migration scenario. 

### Migrating GitHub to GitHub
The quick start video below will help you start migrating between GitHub organizations. 

* Migrating GitHub to GitHub with the GEI CLI: https://youtu.be/5cQkM_8n5YY

### Migrating from Azure DevOps to GitHub
Video guides below will help you get started with your first migration. Then help you build up to orchestrating a complete end-to-end production migration. 
* Running your first few migrations: https://www.youtube.com/watch?v=yfnXbwtXY80
* Orchestrating an end-to-end production migration: https://www.youtube.com/watch?v=AtFB-U1Og4c


## GitHub to GitHub Migration Usage

GEI-CLI is a cross-platform .NET Core console application. General usage will use the `generate-script` option to create a script that can be used to migrate all repositories from a GitHub organization. To get started you'll need to download the official [GitHub CLI](https://cli.github.com). You can run *gh extension install github/gh-gei* to install the GEI CLI. 

### Command line
```
gh-gei
  CLI for GitHub Enterprise Importer.

Usage:
  gh-gei [options] [command]

Options:
  --version       Show version information
  -?, -h, --help  Show help and usage information

Commands:
  generate-script  Generates a migration script. This provides you the ability to review the steps that this tool will take, and optionally 
                   modify the script if desired before running it.
                   Note: Expects GH_SOURCE_PAT or GH_PAT env variable to be set.
  migrate-repo     Invokes the GitHub API's to migrate the repo and all PR data.
                   Note: Expects GH_PAT and GH_SOURCE_PAT env variables to be set. GH_SOURCE_PAT is optional, if not set GH_PAT will be used 
                   instead.
```

To generate a script, you'll need to set an `GH_PAT` as an environment variable for your destination and `GH_SOURCE_PAT` for your source location. 

## Azure DevOps to GitHub Migration Usage

GEI-CLI is a cross-platform .NET Core console application. Execute the executable without any parameters to learn about the options. General usage will use the `generate-script` option to create a script that can be used to migrate all repositories from an Azure DevOps org and re-wire Azure Boards and Azure Pipelines connections.

### Command line
```
ado2gh
  Migrates Azure DevOps repos to GitHub
Usage:
  ado2gh [options] [command]

Options:
  --version       Show version information
  -?, -h, --help  Show help and usage information

Commands:
  generate-script
  rewire-pipeline
  integrate-boards
  share-service-connection
  disable-ado-repo
  lock-ado-repo             Makes the ADO repo read-only for all users. It does this by adding Deny permissions for the Project Valid Users group on the repo.
  configure-auto-link
  create-team               Creates a GitHub team and optionally links it to an IdP group.
  add-team-to-repo
  migrate-repo
```

To generate a script, you'll need to set an `ADO_PAT` as an environment variable. Performing any of the commands that touch GitHub will need the `GH_PAT` environment variable.

### Running a Migration 

Covering running a migration from **Azure DevOps** to **GitHub.com**. 

#### Download and Configure the GH-GEI command-line Tool

Navigate to the `Releases` for this repository and grab the latest release for your local operating system. Note: ado2gh is for Azure DevOps -> GitHub migrations, gei is for GitHub -> GitHub migrations.
![Releases](https://user-images.githubusercontent.com/29484535/145065021-35f37a00-1a25-42a4-804d-11fd9f8cc811.png)
Once you have downloaded the `Release`, you need to extract it to your local machine.
**Note** you will want to place the `octoshift` executable somewhere easy to reference or add to your path.
![Folder View](https://user-images.githubusercontent.com/29484535/145065026-a519a7f0-fc1d-46a1-a1a5-cd96743b1bd1.png)

* [Linux add folder to path](https://linuxize.com/post/how-to-add-directory-to-path-in-linux/)
* [Powershell add folder to path](https://stackoverflow.com/questions/714877/setting-windows-powershell-environment-variables/714918)

Once you have pathed the tooling, you will need to set `2` environment variables. 

* One will be called `GH_PAT` and will be your **GitHub Personal Access Token**
* The other will be called `ADO_PAT` and will be your **Azure DevOps Access Token** 

The scope needed for each token will depend on what command(s) you want run. See the scenarios below to ensure you have properly scoped personal access tokens. It's recommended that you pick the scenario which fits the needs of everything you want to do as part of migrating. 

#### I just want to run some migrations and grant/revoke the migrator role
Create a GitHub and Azure DevOps personal access tokens with the scope defined in GEI's [documentation](https://docs.github.com/en/early-access/github/migrating-with-github-enterprise-importer/migrating-to-github-enterprise-cloud-with-the-importer#step-2-assign-the-migration-permissions-role-and-ensure-the-migrator-has-the-expected-pat-scopes). This will allow you to run these commands:

* generate-script (--repos-only)
* migrate-repo
* grant/revoke-migrator-role
* create-team
* add-team-to-repo
* configure-autolink

#### I want to do the above and also lock & disable the repository being migrated from Azure DevOps
In order to use the following pre & post migration commands:

* lock-ado-repo
* disable-ado-repo
* generate script (without the --repos-only flag)

You will need to include these additional scopes for your Azure DevOps personal access token in addition to the ones listed in GEI's [documentation](https://docs.github.com/en/early-access/github/migrating-with-github-enterprise-importer/migrating-to-github-enterprise-cloud-with-the-importer#step-2-assign-the-migration-permissions-role-and-ensure-the-migrator-has-the-expected-pat-scopes):

* `Service Connection (Read)`
* `Build (Read & execute)`
* `Security (Manage)`
* `Code (Read, write, and manage)`

#### I want to do the above and also re-connect Azure Pipelines & Boards to the newly migrated repository on GitHub
If you want to re-connect an Azure Pipline or Board to the migrated repo then you'll need your ADO personal access token to be `full access`.

![image](https://user-images.githubusercontent.com/40493721/145903240-6a6d04cd-ba03-47f4-84aa-6af741a8ddd6.png)

At this point, you can now begin to run and test the import process.

#### Run the Migrations

Once you have configured the `octoshift`(*gh-gei*) command-line tool and `environment variables` for the person access tokens, you can run the command-line tool and see all available options.
![octoshift help](https://user-images.githubusercontent.com/29484535/145065029-ea8b3fcd-fcea-4f9b-ba7e-f9d3407e17fa.png)

The first step you will want to run is `generate-script` to help outline all commands to migrate an entire **Azure Project**
This command will generate a `migrate.ps1` file in the local folder. You will want to open it with an editor tool as it can be quite large. It's recommended that you include the `--repos-only` flag the first time. 
Tools like `Atom`, `VSCode`, or `NotePad++` are great ways to see the data.

You can then use this as a guide to pick and choose which commands you would like to run.

## Contributions

See [Contributing](CONTRIBUTING.md) for more info on how to get involved.
