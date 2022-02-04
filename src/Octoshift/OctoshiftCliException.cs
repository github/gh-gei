using System;

namespace OctoshiftCLI
{
    public class OctoshiftCliException : Exception
    {
        public OctoshiftCliException()
        {
        }

        public OctoshiftCliException(string message) : base(message)
        {
        }

        public OctoshiftCliException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
