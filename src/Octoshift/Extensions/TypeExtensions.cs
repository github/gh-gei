using System;

namespace OctoshiftCLI.Extensions;

public static class TypeExtensions
{
    internal static T CreateInstance<T>(this Type type) => (T)Activator.CreateInstance(type);
}
