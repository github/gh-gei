using OctoshiftCLI.Commands;

namespace OctoshiftCLI.AdoToGithub.Commands;

public sealed class GenerateMannequinCsvCommand : GenerateMannequinCsvCommandBase
{
    public GenerateMannequinCsvCommand() : base() => AddOptions();
}
