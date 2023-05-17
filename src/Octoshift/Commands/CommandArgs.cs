using System;
using System.Linq;
using System.Text;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.Commands
{
    public abstract class CommandArgs
    {
        public bool Verbose { get; set; }

        public virtual void Validate(OctoLogger log)
        { }

        public void Log(OctoLogger log)
        {
            if (log is null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            log.Verbose = Verbose;

            foreach (var property in GetType().GetProperties())
            {
                var logName = GetLogName(property.Name);

                if (property.PropertyType == typeof(bool))
                {
                    if ((bool)property.GetValue(this))
                    {
                        log.LogInformation($"{logName}: true");
                    }
                }
                else
                {
                    var propValue = property.GetValue(this);

                    if (propValue.HasValue() && propValue.ToString().HasValue())
                    {
                        log.LogInformation($"{logName}: {propValue}");
                    }
                }
            }
        }

        private string GetLogName(string propertyName)
        {
            var result = new StringBuilder();

            foreach (var c in propertyName)
            {
                if (char.IsLower(c))
                {
                    result.Append(char.ToUpper(c));
                }
                else
                {
                    result.Append($" {c}");
                }
            }

            return result.ToString().Trim();
        }

        public void RegisterSecrets(OctoLogger log)
        {
            if (log is null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            foreach (var property in GetType().GetProperties()
                                              .Where(p => p.HasCustomAttribute<SecretAttribute>()))
            {
                log.RegisterSecret((string)property.GetValue(this));
            }
        }
    }
}
