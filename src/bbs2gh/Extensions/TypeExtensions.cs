using System;
using System.Collections.Generic;
using System.Linq;

namespace OctoshiftCLI.BbsToGithub;

public static class TypeExtensions
{
    public static IEnumerable<Type> GetAllDescendantsOfCommandBase(this Type type) =>
        type?.Assembly
            .GetTypes()
            .Where(t =>
                t.IsClass &&
                t.BaseType is { IsGenericType: true } &&
                t.BaseType.GetGenericTypeDefinition() == typeof(CommandBase<,>))
        ?? throw new ArgumentNullException(nameof(type));

    public static T CreateInstance<T>(this Type type) => (T)Activator.CreateInstance(type);
}
