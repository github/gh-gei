using System;

namespace OctoshiftCLI
{
    public static class AdoApiFactory
    {
        public static Func<AdoApi> Create = () =>
        {
            var adoToken = GetAdoToken();
            var client = new AdoClient(adoToken);

            return new AdoApi(client);
        };

        public static Func<string> GetAdoToken = () =>
        {
            var adoToken = Environment.GetEnvironmentVariable("ADO_PAT");

            if (string.IsNullOrWhiteSpace(adoToken))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("ERROR: NO ADO_PAT FOUND IN ENV VARS, exiting...");
                Console.ResetColor();
                return null;
            }

            return adoToken;
        };
    }
}