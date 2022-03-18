using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace OctoshiftCLI.Tests
{
    public static class TestExtensionMethods
    {
        public static IAsyncEnumerable<JToken> ToAsyncJTokenEnumerable<T>(this IEnumerable<T> list) => list.Select(x => JToken.FromObject(x)).ToAsyncEnumerable();
    }
}
