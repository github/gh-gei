using System;
using System.Linq;
using System.Text;

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
                var logName = property.GetCustomAttributes(typeof(LogNameAttribute), true).FirstOrDefault() is LogNameAttribute logNameAttribute
                        ? logNameAttribute.LogName
                        : property.Name;

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

        public void RegisterSecrets(OctoLogger log)
        {
            if (log is null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            foreach (var property in GetType().GetProperties())
            {
                if (property.GetCustomAttributes(typeof(SecretAttribute), true).Any())
                {
                    log.RegisterSecret((string)property.GetValue(this));
                }
            }
        }
    }
}
