using System.Threading.Tasks;

namespace OctoshiftCLI.Commands;

public interface ICommandHandler<in TArgs> where TArgs : CommandArgs
{
    Task Handle(TArgs args);
}
