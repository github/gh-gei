using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace OctoshiftCLI.Extensions
{
    public static class StringExtensions
    {
        public static StringContent ToStringContent(this string s) => new(s, Encoding.UTF8, "application/json");

        public static bool IsNullOrWhiteSpace(this string s) => string.IsNullOrWhiteSpace(s);

        public static bool HasValue(this string s) => !s.IsNullOrWhiteSpace();

        public static bool ToBool(this string s) => bool.TryParse(s, out var result) && result;

        public static string Clean(this string s) => s.HasValue() ? Regex.Replace(s, @"[^a-z0-9_.-]+", "-", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase) : string.Empty;
    }
}
