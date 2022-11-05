using System;

namespace OctoshiftCLI
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class LogNameAttribute : Attribute
    {
        public string LogName { get; private set; }

        public LogNameAttribute(string logName)
        {
            LogName = logName;
        }
    }
}
