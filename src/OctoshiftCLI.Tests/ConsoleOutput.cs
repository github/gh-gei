using System;
using System.IO;

namespace OctoshiftCLI.Tests
{
    public class ConsoleOutput : IDisposable
    {
        private readonly StringWriter stringWriter;
        private readonly TextWriter originalOutput;
        private bool disposedValue;

        public ConsoleOutput()
        {
            stringWriter = new StringWriter();
            originalOutput = Console.Out;
            Console.SetOut(stringWriter);
        }

        public string GetOuput() => stringWriter.ToString();

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Console.SetOut(originalOutput);
                    stringWriter.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}