using System;

namespace OctoshiftCLI.Extensions;

public static class TypeExtensions
{
    public static T CreateInstance<T>(this Type type) => (T)Activator.CreateInstance(type);
}
