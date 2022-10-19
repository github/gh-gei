using System;
using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
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
            var commandBaseType = commandType.BaseType.IsGenericType && commandType.BaseType.GetGenericTypeDefinition() == typeof(CommandBase<,>)
                ? commandType.BaseType
                : commandType.BaseType.BaseType;

            var argsType = commandBaseType.GetGenericArguments()[0];
            var handlerType = commandBaseType.GetGenericArguments()[1];

            var command = (Command)typeof(CommandExtensions)
                .GetMethod("ConfigureCommand", BindingFlags.NonPublic | BindingFlags.Static)
                .MakeGenericMethod(commandType, argsType, handlerType)
                .Invoke(null, new object[] { serviceProvider });

            rootCommand.AddCommand(command);
        }

        return rootCommand;
    }

    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Called via reflection")]
    private static TCommand ConfigureCommand<TCommand, TArgs, THandler>(ServiceProvider sp) where TArgs : class, new()
                                                                                            where TCommand : CommandBase<TArgs, THandler>, new()
                                                                                            where THandler : ICommandHandler<TArgs>
    {
        var command = new TCommand();
        var argsBinder = new GenericArgsBinder<TCommand, TArgs>(command);
        command.SetHandler(async args => await command.BuildHandler(args, sp).Handle(args), argsBinder);
        return command;
    }
}
