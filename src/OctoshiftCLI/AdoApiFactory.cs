using System;

namespace OctoshiftCLI
{
    public static class AdoApiFactory
    {
        public static Func<string, AdoApi> Create = token => new AdoApi(token);
    }
}
