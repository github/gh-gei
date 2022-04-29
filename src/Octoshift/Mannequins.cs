using System;
using System.Linq;

using OctoshiftCLI.Models;

namespace Octoshift
{
    public class Mannequins
    {
        private readonly Mannequin[] _mannequins;
        public Mannequins(Mannequin[] mannequins)
        {
            _mannequins = mannequins;
        }

        public Mannequin FindFirst(string login, string userid)
        {
            return _mannequins.FirstOrDefault(m => login.Equals(m.Login, StringComparison.OrdinalIgnoreCase) && userid.Equals(m.Id, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Checks if the user has been claimed at least once (regardless of the last reclaiming result)
        /// </summary>
        /// <param name="login"></param>
        /// <param name="userid"></param>
        /// <returns></returns>
        public bool IsClaimed(string login, string userid)
        {
            return _mannequins.FirstOrDefault(m =>
                login.Equals(m.Login, StringComparison.OrdinalIgnoreCase) &&
                userid.Equals(m.Id, StringComparison.OrdinalIgnoreCase)
                && m.MappedUser != null)?.Login != null;
        }
    }
}
