using System.Net.Http;
using System.Text;

namespace OctoshiftCLI.Extensions
{
    public static class StringExtensions
    {
        public static StringContent ToStringContent(this string s) => new(s, Encoding.UTF8, "application/json");

        public static bool IsNullOrWhiteSpace(this string s) => string.IsNullOrWhiteSpace(s);

        public static bool ToBool(this string s) => bool.TryParse(s, out var result) && result;
    }
}
