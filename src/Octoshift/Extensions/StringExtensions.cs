using System.Net.Http;
using System.Text;

namespace OctoshiftCLI.Extensions
{
    public static class StringExtensions
    {
        public static StringContent ToStringContent(this string s) => new(s, Encoding.UTF8, "application/json");
    }
}