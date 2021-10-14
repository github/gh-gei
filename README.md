# OctoshiftCLI

The OctoshiftCLI wraps the GitHub Enterprise Importer (GEI, formerly Octoshift) APIs to simplify migrations to GitHub Enterprise.  The current version targets migrations from Azure DevOps to GitHub Enterprise Cloud (with or without EMUs).  @dylan-smith created this version of the OctoshiftCLI including contributions from various members of the [GitHub FastTrack team](https://github.com/github/fasttrack/) (@BrytBoy, @mickeygousset and @tspascoal). OctoshiftCLI was built during real-world FastTrack engagements to facilitate Azure DevOps migrations.

This version of the OctoshiftCLI is informatlly maintained by GitHub, the FastTrack, and @dylan-smith in specifc.  Hubbers are welcome to leverage these tools while helping customers with migrations.  However, **THE OCTOSHIFTCLI IS NOT A SUPPORTED GITHUB PRODUCT!**  Customers leveraging these tools must understand that any support must come through a paid GitHub Expert Services engagement or via FastTrack (limited availability). 

We envision that these capabilities will eventually end up as part of GitHub product, maintained by @roferg and the Octoshift engineering teams.  However for the foreseeable future remember that these are internal and unsupported community tools.

## Usage

OctoshiftCLI is a cross-platform .NET Core console application.  Execute the executable without any parameters to learn about the options. General useage will use the `generate-script` option to create a script that can be used to migrate all repos from an Azure DevOps org and re-wire Azure Boards and Azure Pipelines connections.

TODO - Add something on PATs and other setup

## Contributions

Bring it on! This is a tool built and maintained within GitHub.  Of all developers in the world, Hubbers know the rules and best practices for InnerSource and contributing.
