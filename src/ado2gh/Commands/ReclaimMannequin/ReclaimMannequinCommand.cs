using OctoshiftCLI.Commands;

namespace OctoshiftCLI.AdoToGithub.Commands;

public sealed class ReclaimMannequinCommand : ReclaimMannequinCommandBase
{
    public ReclaimMannequinCommand() : base() => AddOptions();
}
