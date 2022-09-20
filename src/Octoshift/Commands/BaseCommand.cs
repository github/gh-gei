using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;

namespace OctoshiftCLI.Commands
{
    public abstract class BaseCommand<TArgs, THandler> : Command where TArgs : ICommandArgs where THandler : ICommandHandler<TArgs>
    {
        protected BaseCommand(string name, string description = null) : base(name, description) { }

        public virtual THandler BuildHandler(TArgs args, ServiceProvider sp) => sp.GetService<THandler>();
    }
}
