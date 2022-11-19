using System;
using System.Linq;
using System.Reflection;
using System.Text;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI
{
    public abstract class CommandArgs
    {
        [LogName("VERBOSE")]
        public bool Verbose { get; set; }

        public abstract void Validate();

        public void Log(OctoLogger log)
        {
            if (log is null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            log.Verbose = Verbose;
            var sb = new StringBuilder();

            foreach (var property in GetType().GetProperties())
            {
                var logName = GetLogName(property);

                if (property.PropertyType == typeof(bool))
                {
                    if ((bool)property.GetValue(this))
                    {
                        sb.AppendLine($"{logName}: true");
                    }
                }
                else
                {
                    sb.AppendLine($"{logName}: {property.GetValue(this)}");
                }
            }

            log.LogInformation(sb.ToString());
        }

        private string GetLogName(PropertyInfo property)
        {
            return property.GetCustomAttributes(typeof(LogNameAttribute), true).FirstOrDefault() is LogNameAttribute logNameAttribute
                        ? logNameAttribute.LogName
                        : ConvertPropertyNameToLogName(property.Name);
        }

        private string ConvertPropertyNameToLogName(string propertyName)
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
