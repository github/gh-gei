# Progress Log

2025-10-20T19:16Z: Starting implementation of Plan 1 (ruleset support). Preparing to add GithubApi ruleset endpoints and internal model.


2025-10-20T19:18Z: Added preliminary GetRepoRulesets implementation (needs schema validation & robust parsing).

2025-10-20T19:22Z: Fixed syntax error; implemented GetRepoRulesets properly; added and passed initial unit test.

2025-10-20T19:24Z: Added GithubRulesetDefinition model, CreateRepoRuleset method & unit test (passing locally).

2025-10-20T19:26Z: Implemented CreateRepoRuleset with unit test (passing). Fixed earlier insertion issues.

2025-10-20T19:52Z: Added UpdateRepoRuleset + unit test (passing locally before commit).

2025-10-20T20:48Z: Added extraction + apply services with unit tests; updated plan checklist items.

2025-10-20T20:53Z: Added dry-run tests for create/update paths; plan updated.

2025-10-20T20:57Z: Added diff logging to DefaultBranchRulesetService with unit test.

2025-10-20T21:00Z: Added ruleset enable CLI flag provider + tests.

2025-10-20T21:04Z: Added --enable-rulesets CLI option, wiring to CliContext.RulesetsEnabled; README & release notes updated.

2025-10-20T22:25Z: Added unsupported policy exception logic (work item linking, comment resolution) + tests.

2025-10-20T22:30Z: Wired ruleset application into migrate-repo handler (placeholder empty ADO policy set).

2025-10-20T22:36Z: Added AdoBranchPolicyService skeleton + integration test for migrate-repo ruleset application.

2025-10-20T22:40Z: Added placeholder AdoBranchPolicyService integration into migrate-repo handler.

2025-10-20T22:43Z: Temporary AdoApi instantiation in migrate-repo handler for policy retrieval placeholder.

2025-10-20T22:46Z: Added AdoBranchPolicyService real mapping for main branch + unit test.

2025-10-20T22:49Z: Dynamic ADO default branch retrieval implemented with tests.

2025-10-20T22:52Z: Added PR body pattern support for work item linking & comment resolution + tests.

2025-10-20T22:55Z: Added ruleset name conflict suffix logic + status check truncation (50) with tests.

2025-10-20T22:58Z: Injected AdoBranchPolicyService into migrate-repo handler; patterns integration test added.

2025-10-20T23:00Z: Added fallback warning logic + README section + release notes update.

2025-10-20T23:01Z: Added Plan 2 doc for full branch/pattern ruleset migration.
