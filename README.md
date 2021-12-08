# OctoshiftCLI

[![Actions Status: CI](https://github.com/github/octoshiftcli/workflows/CI/badge.svg)](https://github.com/github/octoshiftcli/actions?query=workflow%3ACI)


The OctoshiftCLI wraps the GitHub Enterprise Importer (GEI, formerly Octoshift) APIs to simplify migrations to GitHub Enterprise.  The current version targets migrations from Azure DevOps to GitHub Enterprise Cloud (with or without EMUs).  @

This version of the OctoshiftCLI is informally maintained by GitHub. However, **THIS CLI IS NOT A SUPPORTED GITHUB PRODUCT!**  Customers leveraging these tools must understand that any support must come through a paid GitHub Expert Services engagement.

We envision that these capabilities will eventually end up as part of GitHub product.

## Demo Video

- [OctoshiftCLI Demo](https://www.youtube.com/watch?v=AtFB-U1Og4c)

## Usage

OctoshiftCLI is a cross-platform .NET Core console application.  Execute the executable without any parameters to learn about the options. General usage will use the `generate-script` option to create a script that can be used to migrate all repos from an Azure DevOps org and re-wire Azure Boards and Azure Pipelines connections.

### Command-line
```bash
octoshift
  Migrates Azure DevOps repos to GitHub
Usage:
  octoshift [options] [command]

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


## Prerequisites

- You will need at least `admin` access to the repository on **Azure DevOps**
  - **Note:** Higher permissions may be needed as the tooling progresses
- You will need the ability to `create repositories` in your Organization on **github.com**
  - **Note:** You will need `Org:Admin`, `use:all`, and `repo:all` to use the command-line **GH-GEI** tool that makes a **High Fidelity Migration**
  - **Note:** Your `Personal Access Token` will also need to be authorized for usage against the Organization

## Limitations

Currently, in the `high fidelity` path to migrate, we can *only* migrate over:

- `git` data
- `git` medatadata
- `Teams` and their access rights
- `Webhooks` connected to the repository
- `Issues` connected to the repository
- `Pull requests` connected to the repository
- `Users` (Transformed into **GitHub** mannequins)
- `Azure Pipelines` connected to the repository

> ❗ **Note:** GitHub is currently working towards building a `higher fidelity` migration path, but it is not currently available.

### Import Steps

This method will move the repository and its surrounding metadata from **Azure DevOps** to **GitHub.com**.

#### Download and Configure the GH-GEI command-line Tool

Navigate to the `Releases` for your local operating system.
![Releases](https://user-images.githubusercontent.com/29484535/145065021-35f37a00-1a25-42a4-804d-11fd9f8cc811.png)
Once you have downloaded the `Release`, you need to extract it to your local machine.
**Note** you will want to place the `octoshift` executable somewhere easy to reference or add to your path.
![Folder View](https://user-images.githubusercontent.com/29484535/145065026-a519a7f0-fc1d-46a1-a1a5-cd96743b1bd1.png)

- [Linux add folder to path](https://linuxize.com/post/how-to-add-directory-to-path-in-linux/)
- [Powershell add folder to path](https://stackoverflow.com/questions/714877/setting-windows-powershell-environment-variables/714918)

Once you have pathed the tooling, you will need to set `2` environment variables.
Set the `GH_PAT` to your **GitHub Personal Access Token** that has `org admin` access to the import Organization.
Set the `ADO_PAT` to your **Azure DevOps Access Token** that has `full` access to the Azure Environment

At this point, you can now begin to run and test the import process.

#### Run the Migrations

Once you have configured the `octoshift`(*gh-gei*) command-line tool and `environment variables` for access tokens, you can run the command-line tool and see all available options.
![octoshift help](https://user-images.githubusercontent.com/29484535/145065029-ea8b3fcd-fcea-4f9b-ba7e-f9d3407e17fa.png)

The first step you will want to run is `generate-script` to help outline all commands to migrate an entire **Azure Project**
This command will generate a `octoshift.sh` file in the local folder. You will want to open it with an editor tool as it can be quite large.
Tools like `Atom`, `VSCode`, or `NotePad++` are great ways to see the data.
As you can see from the data, it builds out the command-line options one after another to migrate all repositories, lock repositories, create teams, etc.

You can then use this as a guide to pick and choose which commands you would like to run.

## Contributions

See [Contributing](CONTRIBUTING.md) for more info on how to get involved.
