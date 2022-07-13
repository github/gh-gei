using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace OctoshiftCLI.Tests
{
    public static class TestExtensionMethods
    {
        public static IAsyncEnumerable<JToken> ToAsyncJTokenEnumerable<T>(this IEnumerable<T> list)
            => list.Select(x => JToken.FromObject(x)).ToAsyncEnumerable();

        public static MemoryStream ToStream(this string value)
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(value ?? ""));
        }
    }
}
