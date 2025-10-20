# Plan 2: Full ADO Branch/Pattern Policy Migration to GitHub Rulesets
Date: 2025-10-20
Status: Draft
Owner: (TBD)

## Objective
Extend ruleset support (added in Plan 1) to migrate ALL ADO branch policies (individual branches + folder/pattern scopes like `release/*`) into GitHub rulesets so equivalent protections exist for every branch affected by ADO policies.

## High-Level Approach
1. Enumerate all ADO policy configurations for the repo (not just default branch) via policy configurations API without refName filter.
2. Group configurations by effective scope (exact branch name or pattern/folder). Treat ADO folder style (`release/`) and wildcard (`release/*`) as glob targets for GitHub: `release/**` or `release/*` depending on specificity.
3. For each group, map ADO policies to a GithubRulesetDefinition (reuse extraction logic generalized).
4. Conflict resolution: If overlapping target patterns (e.g., `release/*` and `release/1.0`), create separate rulesets; GitHub applies union. Ensure no contradictory reviewer counts (we always take max per ruleset). Order does not enforce priority—document behavior.
5. Name each ruleset consistently: `ado-branch-{sanitized}` where sanitized converts `/` -> `-`, `*` -> `star`, trims length <= 60; fallback suffix if collision.
6. Apply rulesets idempotently (create/update). Re-run safe.
7. Fail migration if any encountered ADO policy type is unmappable (except external security/review checks -> warning). Work item linking & comment resolution already mapped via body patterns (Plan 1). Merge strategy restrictions remain TODO: evaluate GitHub merge strategy API; if unsupported log warning (not fail per earlier guidance). Auto include reviewers assumed handled by CODEOWNERS existing.
8. Provide summary logging: total groups migrated, list of patterns, any truncations (status checks > limit 50), warnings.
9. Unit tests for grouping, mapping, conflict handling, name collisions, truncation, error on unsupported policy types.

## ADO Policy Scope Parsing
- Each configuration contains a `settings.scope` array with entries having either `refName` (exact branch) or `repositoryRef` / folder path.
- Pattern detection:
  - If scope refName ends with `/*` treat as wildcard group.
  - If a folder path (e.g., `refs/heads/release/`) with inheritance semantics, treat as prefix pattern -> GitHub glob `release/**`.
- Normalize all to glob-ready patterns without `refs/heads/` prefix.

## Mapping Reuse
Generalize `DefaultBranchPolicyExtractionService` to `BranchPolicyExtractionService.BuildRuleset(targetPattern, name, policies)` (rename existing or wrap). Remove implicit default branch assumptions.

## Ruleset TargetPatterns
Single pattern per ruleset for now (simpler). Later enhancement: if multiple exact branches share identical policy sets -> collapse into one ruleset with multiple patterns (optimization optional, out-of-scope for initial PR2).

## Conflict & Overlap Strategy
- Overlap allowed; GitHub will combine requirements. Document that more specific branch (longer literal) gets no special precedence. This is acceptable; union of requirements typically safe.
- If reviewer counts differ between overlapping rulesets, each rule applies; effective requirement = max across triggered rulesets (desired outcome).

## Error Handling
- Unmappable policy types (other than external status/security checks) -> throw OctoshiftCliException and abort.
- External status/security checks with unknown context -> warning; skipped.
- If policy scope cannot be parsed to branch or pattern -> fail.

## Required Code Changes
1. AdoBranchPolicyService: Add method `GetAllPolicies(org, project, repo)` returning list of (ScopePattern, PolicyConfiguration).
2. Introduce model `AdoPolicyScopeGroup { string Pattern; List<AdoPolicyConfiguration> Policies; }` and grouping logic.
3. New service `BranchRulesetMigrationService` orchestrates: fetch -> group -> build -> apply -> log summary.
4. Refactor extraction to `BranchPolicyExtractionService` (rename & update tests) supporting body patterns, truncation, etc.
5. Integrate into `MigrateRepoCommandHandler` after default branch ruleset step (when flag enabled) OR behind second flag `--enable-all-branch-rulesets` (decide: single flag covers both? -> Use existing flag; always migrate all if enabled).
6. Add new unit tests for grouping & naming.
7. Update release notes bullet to reflect expanded coverage when shipped.

## Unit Test Matrix
- Grouping: two branches with different reviewer counts -> two groups.
- Grouping: pattern `release/*` plus branch `release/1.0` -> two groups, patterns sanitized.
- Mapping: work item + comment resolution appear in non-default branch -> body patterns included.
- Name collision: two patterns sanitize to same base -> suffix applied.
- Overlap union: reviewer counts differing; ensure applied rulesets both created.
- Unmappable policy -> throws exception.
- External status check unknown -> warning logged.
- Status check truncation >50 in single group -> truncated + warning.

## Logging
Example summary:
`Ruleset branch coverage: created=5 updated=1 skipped=2 (unchanged). Patterns: main, release/*, hotfix/1.2.3, feature/**`
Warnings listed afterward.

## Checklist
1. [ ] Service: GetAllPolicies
2. [ ] Grouping logic & tests
3. [ ] Extraction service refactor (rename + update old tests)
4. [ ] BranchRulesetMigrationService + tests (create/update paths, name conflict)
5. [ ] Handler integration (flag gate) + integration test
6. [ ] Summary logging + tests
7. [ ] Update docs & release notes
8. [ ] Final review / PR

## Open Questions
- Collapse identical policy sets across many branches into single ruleset? (Optional optimization) -> Defer.
- Support for repo-level merge strategy restrictions? -> Investigate GitHub API; may require separate step outside ruleset.

## Success Criteria
Running migration with rulesets enabled produces rulesets covering every ADO branch/pattern with policies; re-run is idempotent; unmappable policies fail fast with clear message.
