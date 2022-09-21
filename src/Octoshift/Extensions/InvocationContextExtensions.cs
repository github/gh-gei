using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;

namespace OctoshiftCLI.Extensions;

public static class InvocationContextExtensions
{
    internal static object BindArgs(this InvocationContext invocationContext, Command command, Type commandArgsType)
    {
        if (invocationContext is null)
        {
            throw new ArgumentNullException(nameof(invocationContext));
        }

        if (command is null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        if (commandArgsType is null)
        {
            throw new ArgumentNullException(nameof(commandArgsType));
        }

        var args = Activator.CreateInstance(commandArgsType);

        foreach (var prop in command.GetType().GetProperties().Where(p => p.GetValue(command) is Option))
        {
            args.GetType()
                .GetProperty(prop.Name)?
                .SetValue(args, invocationContext.ParseResult.GetValueForOption((Option)prop.GetValue(command)!));
        }

        return args;
    }
}
