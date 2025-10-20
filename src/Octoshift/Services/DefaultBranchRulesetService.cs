using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OctoshiftCLI.Models;

namespace OctoshiftCLI.Services
{
    public class DefaultBranchRulesetService
    {
        private readonly GithubApi _github;
        private readonly OctoLogger _log;
        public DefaultBranchRulesetService(GithubApi github, OctoLogger log){ _github=github; _log=log; }

        public async Task<int> Apply(string org, string repo, GithubRulesetDefinition def, bool dryRun)
        {
            if(def==null) throw new OctoshiftCliException("Ruleset definition cannot be null");
            var existing = await _github.GetRepoRulesets(org, repo);
            var match = existing.FirstOrDefault(r => r.Name == def.Name);
            if(match.Name != null)
            {
                LogDiff(match, def);
                if(IsEquivalent(match, def)) { _log.LogInformation("Ruleset unchanged"); return match.Id; }
                if(dryRun) { _log.LogInformation("DRY-RUN: Would update ruleset"); return match.Id; }
                await _github.UpdateRepoRuleset(org, repo, match.Id, def);
                _log.LogInformation("Updated ruleset");
                return match.Id;
            }
            if(dryRun){ _log.LogInformation("DRY-RUN: Would create ruleset"); return 0; }
            var id = await _github.CreateRepoRuleset(org, repo, def);
            _log.LogInformation("Created ruleset");
            return id;
        }

        private void LogDiff((int Id,string Name,IEnumerable<string> TargetPatterns,int? RequiredApprovingReviewCount,IEnumerable<string> RequiredStatusChecks) existing, GithubRulesetDefinition def)
        {
            if(existing.RequiredApprovingReviewCount != def.RequiredApprovingReviewCount)
                _log.LogInformation($"Approving review count: {existing.RequiredApprovingReviewCount} -> {def.RequiredApprovingReviewCount}");
            var existingChecks = existing.RequiredStatusChecks.OrderBy(x=>x).ToArray();
            var newChecks = def.RequiredStatusChecks.OrderBy(x=>x).ToArray();
            var added = newChecks.Except(existingChecks).ToArray();
            var removed = existingChecks.Except(newChecks).ToArray();
            if(added.Any()) _log.LogInformation("Added status checks: " + string.Join(",", added));
            if(removed.Any()) _log.LogInformation("Removed status checks: " + string.Join(",", removed));
        }

        private static bool IsEquivalent((int Id,string Name,IEnumerable<string> TargetPatterns,int? RequiredApprovingReviewCount,IEnumerable<string> RequiredStatusChecks) existing, GithubRulesetDefinition def) =>
            existing.RequiredApprovingReviewCount==def.RequiredApprovingReviewCount &&
            existing.TargetPatterns.SequenceEqual(def.TargetPatterns) &&
            existing.RequiredStatusChecks.OrderBy(x=>x).SequenceEqual(def.RequiredStatusChecks.OrderBy(x=>x));
    }
}
