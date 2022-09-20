using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;

namespace OctoshiftCLI.BbsToGithub;

public abstract class CommandBase<TArgs, THandler> : Command where TArgs : class where THandler : class
{
    protected CommandBase(string name, string description = null) : base(name, description) { }

    public abstract THandler BuildHandler(TArgs args, ServiceProvider sp);
}
