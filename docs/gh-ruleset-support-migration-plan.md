# Plan 1: Introduce GitHub Ruleset Support (Default Branch Policy Migration Modernization)

Date: 2025-10-20
Owner: (TBD)
Status: Draft

## Objective
Replace legacy branch protection updates with creation/update of a single GitHub repository ruleset representing the ADO policies currently migrated for the default branch (initially required status checks / reviewers). Provide parity plus foundation for broader branch/pattern coverage (Plan 2).

## Scope (PR 1)
In-scope:
- Add minimal GithubApi ruleset endpoints (list, create, update).
- Map existing migrated default branch policies to ruleset fields.
- Idempotent behavior (no duplicate rulesets; update when diff).
- Dry-run support.
- Feature flag / opt-out (env var or CLI arg) during transition.
Out-of-scope:
- Non-default branch patterns.
- Additional ADO policy types beyond current default branch subset.

## Detailed ADO -> GitHub Ruleset Mapping

Supported in PR1 (default branch only):
1. Required reviewers (ADO Minimum number of reviewers policy)
2. Required status checks (ADO Build validation / Status policy that produces a check)
3. (Optional if easily derivable) Block direct pushes (Require pull request) -> implicit when ruleset enforces required reviewers.

Additional ADO Policies (must attempt mapping; failure -> error & abort):
- Work item linking (ADO: Require linked work items) -> GitHub: No direct equivalent; propose failure with clear message until GH provides rule (hard error if detected).
- Comment requirements (e.g., require a linked work item or comment resolution) -> Partial: GitHub has "Required conversation resolution" (branch protection legacy); need ruleset parity (if ruleset API exposes). If unavailable -> hard error.
- Automatically include code reviewers -> Evaluate if GitHub CODEOWNERS can be auto-populated; if not implement TODO and fail.
- Merge strategy restrictions (e.g., squash only) -> Map to repository merge settings (not ruleset). If cannot set -> hard error.
- Linked review/security checks (e.g., Sonar, custom) -> Treat as status checks; if context unknown -> fail.

Policy presence without successful mapping MUST throw OctoshiftCliException (no warnings).

Mapping Table (Initial):
| ADO Policy | ADO Fields | GitHub Ruleset Rule | Notes |
|------------|------------|---------------------|-------|
| Minimum reviewers | settings.minimumApproverCount | required_approving_review_count | Use max if multiple apply |
| Build validation (status checks) | settings.buildDefinitionId / displayName | required_status_checks.contexts[] | Need mapping from build to status context; user may supply explicit mapping if ambiguous |
| Require pull request (implicit) | scope with refName only | required_approving_review_count>=1 | Do not create separate rule; reviewers rule covers |

Conflict Resolution:
- Reviewers: take highest minimumApproverCount.
- Status checks: union of all contexts.
- Disabled policies: ignore.
- Unmappable policies: throw before apply (no partial apply).

Open Data Needed:
- Exact ADO policy type IDs for minimum reviewers and build validation.
- Status check context naming conventions for build validation (may appear as Azure Pipelines check: "Azure Pipelines" + pipeline name). Provide heuristic extraction.

Testing Matrix (Unit Tests):
1. Only reviewers policy -> ruleset reviewers count set correctly.
2. Reviewers + build validation -> union mapping.
3. Multiple reviewer policies different counts -> highest applied.
4. Disabled build validation -> excluded.
5. Unmappable policy (e.g., work item linking) -> throws OctoshiftCliException, no ruleset created.
6. Existing ruleset identical -> no update.
7. Existing ruleset missing one status check -> update adds check.
8. Truncation scenario when > N checks (simulate limit) -> throws (hard error), not silent.
9. Mixed mappable + unmappable -> overall failure (no partial apply).

## Steps / Checklist
### 1. API Layer
- [ ] GithubApi: GetRepoRulesets(org, repo)
- [ ] GithubApi: CreateRepoRuleset(org, repo, def)
- [ ] GithubApi: UpdateRepoRuleset(org, repo, id, def)
- [ ] Internal model GithubRulesetDefinition { Name, TargetPatterns[], RequiredApprovingReviewCount, RequiredStatusChecks[], Enforcement }.

### 2. Extract Existing Mapping Logic
- [ ] Identify current code deriving status checks / reviewers from ADO (reuse existing extraction).
- [ ] Encapsulate into service DefaultBranchPolicyExtractionService.

### 3. Ruleset Construction
- [ ] Build ruleset name: ado-default-branch-policies.
- [ ] Target pattern = exact default branch name.
- [ ] Populate rules: reviewers, status checks.
- [ ] Enforcement: active; bypass = none (initial).

### 4. Idempotent Apply Service
- [ ] Implement DefaultBranchRulesetService.Apply(org, repo, defBranch, reviewers, checks, dryRun, enableRulesets).
- [ ] If rulesets disabled -> fallback to existing branch protection behavior (legacy path preserved).
- [ ] If enabled -> ensure ruleset exists/updated; remove legacy branch protection only if safe (optional phase 2, skip removal for first PR).

### 5. CLI Integration
- [ ] Add global argument `--enable-rulesets` (default false) OR env var `OCTOSHIFT_ENABLE_RULESETS`.
- [ ] Help text update.

### 6. Logging
- [ ] Log diff summary (added/removed checks, reviewer count change).
- [ ] Warn if ruleset API unavailable / PAT scope insufficient and fallback occurs.

### 7. Testing
- [ ] Unit tests: ruleset diff logic (no-op vs update).
- [ ] Unit tests: feature flag behavior.
- [ ] Unit tests: mapping from extracted policies to ruleset model.
- [ ] (Optional) Integration test with mocked HTTP.

### 8. Documentation & Release Notes
- [ ] New doc (this file) referenced by broader Plan 2 doc.
- [ ] RELEASENOTES.md bullet: "Added optional GitHub ruleset support for default branch policy migration (enable via --enable-rulesets)."
- [ ] README: brief section on enabling rulesets.

### 9. Edge Cases
- Missing default branch name -> error.
- Existing ruleset with conflicting name but different target -> create new with suffix.
- Status check contexts > GitHub limit -> log and truncate (document limit).

### 10. Rollout Strategy
- Ship behind flag.
- After validation, switch flag default to true in later release.

## Open Questions
- Remove legacy branch protection automatically? (defer)
- Need ability to set bypass actors? (future)

## Success Criteria
- Running migration with flag produces a ruleset visible in repo settings matching prior branch protection configuration.
- Re-running is idempotent.
- Legacy path untouched when flag disabled.
