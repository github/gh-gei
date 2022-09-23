using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.Handlers;

namespace OctoshiftCLI.Commands;

public abstract class CommandBase<TArgs, THandler> : Command where TArgs : class where THandler : ICommandHandler<TArgs>
{
    protected CommandBase(string name, string description = null) : base(name, description) { }

    public abstract THandler BuildHandler(TArgs args, ServiceProvider sp);
}
