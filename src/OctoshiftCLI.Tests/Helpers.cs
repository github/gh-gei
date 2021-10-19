using System.CommandLine;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using System;
using Xunit;

namespace OctoshiftCLI.Tests
{
    public class Helpers
    {
        public static void VerifyCommandOption(IReadOnlyList<Option> options, string name, bool required)
        {
            var option = options.Single(x => x.Name == name);
            
            Assert.Equal(required, option.IsRequired);
        }


        // NOTE: The following method is derived from: https://jackma.com/2019/04/20/execute-a-bash-script-via-c-net-core/
        public static Task<int> Bash(string cwd, string cmd)
        {
            var source = new TaskCompletionSource<int>();
            var escapedArgs = cmd.Replace("\"", "\\\"");
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "bash",
                    Arguments = $"-c \"{escapedArgs}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = cwd,
                    UseShellExecute = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            process.Exited += (sender, args) =>
            {
                Debug.WriteLine(process.StandardOutput.ReadToEnd());
                Console.WriteLine(process.StandardError.ReadToEnd());
                if (process.ExitCode == 0)
                {
                    source.SetResult(0);
                }
                else
                {
                    source.SetException(new Exception($"Command `{cmd}` failed with exit code `{process.ExitCode}`"));
                }

                process.Dispose();
            };

            try
            {
                process.Start();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Command {cmd} failed with {e.Message}");
            }

            return source.Task;
        }
    }
}
