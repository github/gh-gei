using System;
using System.CommandLine;
using System.CommandLine.Binding;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Handlers;

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
            var argsType = commandType.BaseType.GetGenericArguments()[0];
            var handlerType = commandType.BaseType.GetGenericArguments()[1];

            var command = (Command)typeof(CommandExtensions)
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                .Single(m => m.Name == "ConfigureCommand" && m.IsGenericMethod && m.GetParameters().Length == 3)
                .MakeGenericMethod(commandType, argsType, handlerType)
                .Invoke(null, new object[] { serviceProvider });

            rootCommand.AddCommand(command);
        }

        return rootCommand;
    }

    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Called via reflection")]
    private static TCommand ConfigureCommand<TCommand, TArgs, THandler>(ServiceProvider sp) where TArgs : class, ICommandArgs, new() where TCommand : CommandBase<TArgs, THandler>, new() where THandler : ICommandHandler<TArgs>
    {
        var command = new TCommand();
        var argsBinder = new GenericArgsBinder<TCommand, TArgs>(command);
        command.SetHandler(async args => await command.BuildHandler(args, sp).Handle(args), argsBinder);
        return command;
    }
}
