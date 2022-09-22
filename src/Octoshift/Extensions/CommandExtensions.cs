using System;
using System.CommandLine;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace OctoshiftCLI.Extensions;

public static class CommandExtensions
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static RootCommand AddCommands(this RootCommand rootCommand, ServiceProvider serviceProvider)
    {
        if (rootCommand is null)
        {
            throw new ArgumentNullException(nameof(rootCommand));
        }

        foreach (var commandType in Assembly.GetCallingAssembly().GetAllDescendantsOfCommandBase())
        {
            var command = commandType.CreateInstance<Command>();

            command.SetHandler(async ctx =>
            {
                var commandArgsType = commandType.BaseType.GetGenericArguments()[0];
                var commandArgs = ctx.BindArgs(command, commandArgsType);
                var handler = commandType.GetMethod("BuildHandler").Invoke(command, new[] { commandArgs, serviceProvider });
                await (Task)handler.GetType().GetMethod("Handle").Invoke(handler, new[] { commandArgs });
            });

            rootCommand.AddCommand(command);
        }

        return rootCommand;
    }
}
