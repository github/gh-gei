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
