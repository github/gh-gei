//using System;
//using System.Collections.Generic;
//using System.Linq;

//using OctoshiftCLI.Models;

//namespace Octoshift.Models
//{
//    public class Mannequins
//    {
//        private readonly Mannequin[] _mannequins;

//        public Mannequins(IEnumerable<Mannequin> mannequins)
//        {
//            _mannequins = mannequins.ToArray();
//        }

//        public Mannequin FindFirst(string login, string userid)
//        {
//            return _mannequins.FirstOrDefault(m => login.Equals(m.Login, StringComparison.OrdinalIgnoreCase) && userid.Equals(m.Id, StringComparison.OrdinalIgnoreCase));
//        }

//        /// <summary>
//        /// Gets all mannequins by login and (optionally by login and user id)
//        /// </summary>
//        /// <param name="mannequinUser"></param>
//        /// <param name="mannequinId">null to ignore</param>
//        /// <returns></returns>
//        internal IEnumerable<Mannequin> GetByLogin(string mannequinUser, string mannequinId)
//        {
//            return _mannequins.Where(
//                    m => mannequinUser.Equals(m.Login, StringComparison.OrdinalIgnoreCase) &&
//                        (mannequinId == null || mannequinId.Equals(m.Id, StringComparison.OrdinalIgnoreCase))
//                );
//        }

//        /// <summary>
//        /// Checks if the user has been claimed at least once (regardless of the last reclaiming result)
//        /// </summary>
//        /// <param name="login"></param>
//        /// <param name="id"></param>
//        /// <returns></returns>
//        public bool IsClaimed(string login, string id)
//        {
//            return _mannequins.FirstOrDefault(m =>
//                login.Equals(m.Login, StringComparison.OrdinalIgnoreCase) &&
//                id.Equals(m.Id, StringComparison.OrdinalIgnoreCase)
//                && m.MappedUser != null)?.Login != null;
//        }

//        public bool IsClaimed(string login)
//        {
//            return _mannequins.FirstOrDefault(m =>
//                login.Equals(m.Login, StringComparison.OrdinalIgnoreCase)
//                && m.MappedUser != null)?.Login != null;
//        }

//        public bool IsEmpty()
//        {
//            return _mannequins.Length == 0;
//        }

//        public IEnumerable<Mannequin> GetUniqueUsers()
//        {
//            return _mannequins.DistinctBy(x => $"{x.Id}__{x.Login}");
//        }
//    }
//}
