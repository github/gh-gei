using System;
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

        public static string ReplaceInvalidCharactersWithDash(this string s) => s.HasValue() ? Regex.Replace(s, @"[^\w.-]+", "-", RegexOptions.Compiled | RegexOptions.CultureInvariant) : string.Empty;

        public static string ToWindowsPath(this string path) => path?.Replace("/", "\\");

        public static string ToUnixPath(this string path) => path?.Replace("\\", "/");

        public static string EscapeDataString(this string value) => Uri.EscapeDataString(value);

        public static byte[] ToBytes(this string s) => Encoding.UTF8.GetBytes(s);

        public static bool IsUrl(this string s)
        {
            return !s.IsNullOrWhiteSpace()
                && Uri.TryCreate(s, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        public static bool IsProximaApiUrl(this string s)
        {
            return Uri.TryCreate(s?.Trim(), UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                && Uri.CheckHostName(uri.Host) == UriHostNameType.Dns
                && Regex.IsMatch(uri.Host, @"^api\.(?:[^.]+\.)+ghe\.com$", RegexOptions.IgnoreCase)
                && uri.AbsolutePath == "/"
                && string.IsNullOrEmpty(uri.Query)
                && string.IsNullOrEmpty(uri.Fragment)
                && string.IsNullOrEmpty(uri.UserInfo);
        }

        // Extracts the base (web) URL from a GitHub API URL. Handles both the GHES template
        // (http(s)://hostname/api/v3) and the GHE.com/Proxima template (http(s)://api.hostname).
        // Falls back to returning the input unchanged when neither template matches.
        public static string ExtractGitHubBaseUrl(this string apiUrl)
        {
            apiUrl = apiUrl.Trim().TrimEnd('/');

            var baseUrl = Regex.Match(apiUrl, @"(?<baseUrl>https?:\/\/.+)\/api\/v3", RegexOptions.IgnoreCase).Groups["baseUrl"].Value;
            if (baseUrl.HasValue())
            {
                return baseUrl;
            }

            var match = Regex.Match(apiUrl, @"(?<scheme>https?):\/\/api\.(?<host>.+)", RegexOptions.IgnoreCase);
            return match.Success ? $"{match.Groups["scheme"]}://{match.Groups["host"]}" : apiUrl;
        }
    }
}
