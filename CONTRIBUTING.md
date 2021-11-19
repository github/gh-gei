We encourage anybody to contribute to this project. Whether that is submitting bug reports or feature requests, providing feedback in Discussions, or actually writing and contributing code.

Here's some helpful notes on how to contribute to this project, including details on how to get started working the codebase.

## How to submit a bug or request a feature
If you think you've found a bug or have a great idea for new functionality please create an Issue in this repo. We have Issue Templates for both Bugs and Feature Requests.

## How to provide feedback or ask for help
Use the [Discussions](https://github.com/github/OctoshiftCLI/discussions) tab in this repo for more general feedback or any questions/comments on this tooling.

## Product Backlog
All work done by the maintainers of this repo is tracked in this repo using Issues. We have a hierarchical backlog with Epics at the top, broken down into Batches then broken down to Tasks (epic/batch/task is indicated via labels on the issues). You can see an example Epic and navigate down from there [here](https://github.com/github/OctoshiftCLI/issues/101).

## Submitting a Pull Request
Before submitting a Pull Request please first open an issue to get feedback on the change you intend to submit. There's no worse feeling than submitting a PR after alot of effort only to learn that the maintainers don't intend to add that feature to the product (or have very different ideas on how it should be designed).

When creating a PR the template will prompt you to confirm that you have done various required steps (or at least considered them and determined they are not necessary on this PR):

1. Most code should include unit tests (and sometimes e2e tests). New features should include new tests in the same PR. And changes to existing behaviour should update the relevant tests.

2. If this change is something that users should be notified about (e.g. most bug fixes and new features - but probably not code refactorings) be sure to add one or more bullets to the `RELEASENOTES.md` file. The contents of this file will automatically be included in the next release.

3. Consider whether the code changes should have any additional (or changed) log output and be sure those logging changes are included in the same PR.

4. Most PR's should be linked to one or more relevant issues that they implement.

For more info on how to get started contributing code see [this doc](docs/ContributingCode.md).