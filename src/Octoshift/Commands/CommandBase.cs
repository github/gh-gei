using System;
using System.CommandLine;

namespace OctoshiftCLI.Commands;

public abstract class CommandBase<TArgs, THandler> : Command where TArgs : CommandArgs where THandler : ICommandHandler<TArgs>
{
    protected CommandBase(string name, string description = null) : base(name, description) { }

    public abstract THandler BuildHandler(TArgs args, IServiceProvider sp);
}
