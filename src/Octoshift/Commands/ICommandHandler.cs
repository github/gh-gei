using System.Threading.Tasks;

namespace OctoshiftCLI.Handlers;

public interface ICommandHandler<in TArgs> where TArgs : class
{
    Task Handle(TArgs args);
}
