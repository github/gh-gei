using System.Threading.Tasks;

namespace OctoshiftCLI.Commands;

public interface ICommandHandler<in TArgs> where TArgs : ICommandArgs
{
    Task Invoke(TArgs args);
}
