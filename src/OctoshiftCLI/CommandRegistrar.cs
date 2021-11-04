using System.CommandLine;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.Commands;

namespace OctoshiftCLI
{
    public static class CommandRegistrar
    {
        public static IServiceCollection AddCommands(this IServiceCollection services)
        {
            var sampleCommandType = typeof(GenerateScriptCommand);
            var commandType = typeof(Command);

            var commands = sampleCommandType
                .Assembly
                .GetExportedTypes()
                .Where(x => x.Namespace == sampleCommandType.Namespace && commandType.IsAssignableFrom(x));

            foreach (var command in commands)
            {
                services.AddSingleton(commandType, command);
            }

            return services;
        }
    }
}
