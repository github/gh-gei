using System.Threading.Tasks;

namespace OctoshiftCLI.Commands;

public interface ICommandHandler<in TArgs> where TArgs : class
{
    Task Handle(TArgs args);
}
