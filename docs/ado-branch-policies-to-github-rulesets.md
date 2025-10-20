# ADO Branch Policies -> GitHub Rulesets Migration Plan

Date: 2025-10-20
Owner: (TBD)
Status: Draft

## Goal
Migrate all applicable Azure DevOps (ADO) branch policies (including pattern-based policies like `release/*`) to equivalent GitHub repository rulesets so that any protected ADO branch or branch pattern ends up with an equivalent protection configuration in GitHub.

## High-Level Flow (Plan 2 depends on Plan 1 ruleset support)
1. Ensure Plan 1 ruleset capability (list/create/update) exists and is enabled (flag may need to be on).
2. Discover ADO branch policies (including pattern / folder scoped).
3. Aggregate & normalize policies per explicit branch and per pattern.
4. Map ADO policy types to GitHub ruleset settings.
5. Determine required GitHub rulesets (create or update) per branch/pattern.
6. Apply rulesets (idempotent) with dry-run support.
7. Report summary.

## Incremental Component Breakdown / Checklist

### 1. Data Retrieval
- [ ] Add AdoApi method: `GetPolicyConfigurations(org, project)` -> raw configurations (GET `https://dev.azure.com/{org}/{project}/_apis/policy/configurations?api-version=7.0`)
- [ ] Filter for repository-scoped policies (scope settings include `repositoryId`, `refName` or `matchKind`).
- [ ] Extract fields: policy type id, settings (min reviewers, build validation, status checks), refName or pattern.
- [ ] Handle pattern policies (e.g., `refs/heads/release/*`).

### 2. Domain Models
- [ ] Create model `AdoPolicyConfiguration` (raw normalized record).
- [ ] Create aggregation model `BranchPolicyAggregate` with: `Patterns[]`, `ExactBranches[]`, `MinReviewers`, `StatusChecks[]`, `OtherUnsupported[]`.
- [ ] Logic to merge multiple policies applying to same branch/pattern (e.g., min reviewers + build validation stacked).
- [ ] Conflict resolution rules (e.g., highest MinReviewers wins; union of StatusChecks).

### 3. Normalization
- [ ] Strip `refs/heads/` prefix.
- [ ] Convert ADO wildcard patterns to GitHub ruleset target conditions (keep `release/*` as is; ensure GitHub format supports `fnmatch`).
- [ ] Distinguish explicit branch names vs patterns.
- [ ] Deduplicate overlapping (exact overrides pattern when both exist).

### 4. Mapping to GitHub Rulesets
- [ ] Minimum reviewers -> `required_approving_review_count`.
- [ ] Build validation policies -> translate to required status checks (derive status context name; confirm available naming conventions / pipeline result context).
- [ ] Other ADO policy types (e.g., comment requirements) initially ignored; log warning.
- [ ] Compose `GithubRulesetDefinition` (new model) with enforcement level, bypass options (defaults), conditions (target refs), and rules.

### 5. GitHub API Extension
- [ ] Add GithubApi methods: `GetRepoRulesets(org, repo)` and `CreateOrUpdateRuleset(org, repo, ruleset)` (use REST previews if needed).
- [ ] Implement idempotent create/update: match existing by name or by exact target conditions; update only if diff.
- [ ] Support dry-run flag (log intended changes without calling mutation endpoints).

### 6. Service Layer
- [ ] Implement `BranchPolicyRulesetService` orchestrating steps 1-5.
- [ ] Provide public method `ApplyBranchPolicies(org, project, adoRepoId, ghOrg, ghRepo, dryRun)`.
- [ ] Inject via DI and wire into generate / migrate command pipeline (identify correct command to extend or create new command `migrate-branch-policies`).

### 7. CLI Integration
- [ ] Decide integration point: augment existing migration command or new command.
- [ ] Add new command args: `--include-branch-policies` or dedicated command with `--dry-run`.
- [ ] Help text + README snippet.

### 8. Logging & Diagnostics
- [ ] Detailed log for each discovered policy (raw -> mapped).
- [ ] Warnings for unsupported policy types.
- [ ] Summary: counts of branches, patterns, rulesets created/updated/skipped.

### 9. Testing
- [ ] Unit tests for AdoApi parsing (mock JSON samples).
- [ ] Unit tests for aggregation & conflict resolution.
- [ ] Unit tests for mapping logic (policy -> ruleset diff).
- [ ] Unit tests for idempotent update (no change when same; update when different).
- [ ] Integration test (if feasible) with fixtures (may need API stubs).

### 10. Documentation & Release Notes
- [ ] Update README (feature description & usage example).
- [ ] Add entry to `RELEASENOTES.md`.
- [ ] Add doc section linking this file.

### 11. Edge Cases / Considerations
- Patterns overlapping (e.g., `release/*` and `release/hotfix/*`).
- Branch names containing slashes beyond pattern.
- Multiple build validations (aggregate all status checks).
- Policy disabled state (skip if disabled).
- Enforcement level differences (ADO vs GitHub) – start with `active` only.

### 12. Future Enhancements (Backlog)
- Map additional ADO policy types (e.g., require linked work items, comment resolution).
- Support organization-level rulesets.
- Bypass actor mapping (admins, specific teams).
- Configurable naming scheme for generated rulesets.

## Data Mapping Reference (Initial)
| ADO Policy Type | GitHub Ruleset Field | Notes |
|-----------------|----------------------|-------|
| Minimum reviewers (PolicyTypeId: TBD) | required_approving_review_count | Use max of multiple configs |
| Build validation / Status (PolicyTypeId: TBD) | required_status_checks.contexts[] | Derive status context name |
| Others (Work Item linking, etc.) | (Not mapped) | Warn |

## Naming Convention
Ruleset name pattern: `ado-branch-policy:<branch-or-pattern>` (sanitize `/` to `_` except wildcard `*`).

## Open Questions
- Exact PolicyTypeIds to include (confirm via sample JSON).
- GitHub ruleset API version & availability in current PAT scopes.
- Status check context derivation from ADO build validation (may require user-supplied mapping?).

## Progress Tracking
Use checkboxes above; update as tasks complete.
