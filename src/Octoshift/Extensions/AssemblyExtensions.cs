using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OctoshiftCLI.Commands;

namespace OctoshiftCLI.Extensions;

public static class AssemblyExtensions
{
    internal static IEnumerable<Type> GetAllDescendantsOfCommandBase(this Assembly assembly) =>
        assembly?
            .GetTypes()
            .Where(t =>
                t.IsClass &&
                t.BaseType is { IsGenericType: true } &&
                t.BaseType.GetGenericTypeDefinition() == typeof(CommandBase<,>))
        ?? throw new ArgumentNullException(nameof(assembly));
}
