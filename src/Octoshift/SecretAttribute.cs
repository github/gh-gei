using System;

namespace OctoshiftCLI
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class SecretAttribute : Attribute
    {
    }
}
