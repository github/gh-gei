# OctoshiftCLI

[![Actions Status: CI](https://github.com/github/octoshiftcli/workflows/CI/badge.svg)](https://github.com/github/octoshiftcli/actions?query=workflow%3ACI)


The OctoshiftCLI wraps the GitHub Enterprise Importer (GEI, formerly Octoshift) APIs to simplify migrations to GitHub Enterprise.  The current version targets migrations from Azure DevOps to GitHub Enterprise Cloud (with or without EMUs).  @

This version of the OctoshiftCLI is informally maintained by GitHub. However, **THIS CLI IS NOT A SUPPORTED GITHUB PRODUCT!**  Customers leveraging these tools must understand that any support must come through a paid GitHub Expert Services engagement.

We envision that these capabilities will eventually end up as part of GitHub product.

## Demo Video

https://www.youtube.com/watch?v=AtFB-U1Og4c

## Usage

OctoshiftCLI is a cross-platform .NET Core console application.  Execute the executable without any parameters to learn about the options. General usage will use the `generate-script` option to create a script that can be used to migrate all repos from an Azure DevOps org and re-wire Azure Boards and Azure Pipelines connections.

### Command line
```
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

## Contributions

See [Contributing](CONTRIBUTING.md) for more info on how to get involved.