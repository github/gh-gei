using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OctoshiftCLI.Extensions
{
    public static class EnumerableExtensions
    {
        public static async Task<int> Sum<T>(this IEnumerable<T> list, Func<T, Task<int>> selector)
        {
            var result = 0;

            if (list is not null && selector is not null)
            {
                foreach (var item in list)
                {
                    result += await selector(item);
                }
            }

            return result;
        }

        public static IEnumerable<T> ToEmptyEnumerableIfNull<T>(this IEnumerable<T> enumerable) => enumerable ?? Enumerable.Empty<T>();

        public static string GetString(this byte[] bytes) => Encoding.UTF8.GetString(bytes.ToArray());
    }
}
