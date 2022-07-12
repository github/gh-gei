using System;
using System.Collections.Generic;
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

        public static ulong? ToULongOrNull(this string s) => ulong.TryParse(s, out var result) ? result : null;

        public static IEnumerable<string> Lines(this string input) => input?.Split(new string[] { Environment.NewLine, "\n" }, StringSplitOptions.RemoveEmptyEntries);

        public static string ReplaceInvalidCharactersWithDash(this string s) => s.HasValue() ? Regex.Replace(s, @"[^\w.-]+", "-", RegexOptions.Compiled | RegexOptions.CultureInvariant) : string.Empty;
    }
}
