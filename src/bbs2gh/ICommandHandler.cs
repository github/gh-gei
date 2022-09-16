using System.Threading.Tasks;

namespace OctoshiftCLI.BbsToGithub;

public interface ICommandHandler<in TArgs> where TArgs: class
{
    Task Handle(TArgs args);
}
