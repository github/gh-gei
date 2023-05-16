using System;

namespace OctoshiftCLI.Commands
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class SecretAttribute : Attribute
    {
    }
}
