using System;
using System.Collections.Generic;
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

        public Mannequins(IEnumerable<Mannequin> mannequins)
        {
            _mannequins = mannequins.ToArray();
        }

        public Mannequin FindFirst(string login, string userid)
        {
            return _mannequins.FirstOrDefault(m => login.Equals(m.Login, StringComparison.OrdinalIgnoreCase) && userid.Equals(m.Id, StringComparison.OrdinalIgnoreCase));
        }

        internal IEnumerable<Mannequin> GetByLogin(string mannequinUser)
        {
            return _mannequins.Where(m => mannequinUser.Equals(m.Login, StringComparison.OrdinalIgnoreCase));
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

        public bool IsClaimed(string login)
        {
            return _mannequins.FirstOrDefault(m =>
                login.Equals(m.Login, StringComparison.OrdinalIgnoreCase)
                && m.MappedUser != null)?.Login != null;
        }

        public bool Empty()
        {
            return _mannequins.Length == 0;
        }

        public IEnumerable<Mannequin> UniqueUsers()
        {
            return _mannequins.DistinctBy(x => $"{x.Id}__{x.Login}");
        }

        //private IEnumerable<string> GetMappedTo(string login, string userid)
        //{
        //    return _mannequins.Where(m =>
        //            login.Equals(m.Login, StringComparison.OrdinalIgnoreCase) &&
        //            userid.Equals(m.Id, StringComparison.OrdinalIgnoreCase)
        //            && m.MappedUser != null)
        //        .Select(m => m.MappedUser.Login)
        //        .ToList();
        //}
    }
}
