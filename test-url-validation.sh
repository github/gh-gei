#!/bin/bash

echo "================================================================================"
echo "           URL VALIDATION FUNCTIONAL TEST SUITE - Issue #1180                  "
echo "================================================================================"
echo ""

PASS=0
FAIL=0

run_test() {
    local test_num=$1
    local description=$2
    local command=$3
    local expected_error=$4
    
    echo "--------------------------------------------------------------------------------"
    echo "Test $test_num: $description"
    echo "Command: $command"
    echo "--------------------------------------------------------------------------------"
    
    output=$(eval "$command" 2>&1)
    
    if [ -n "$expected_error" ]; then
        if echo "$output" | grep -q -F "$expected_error"; then
            echo "[PASS] Error detected correctly"
            echo "   $(echo "$output" | grep "\[ERROR\]" | head -1)"
            PASS=$((PASS + 1))
        else
            echo "[FAIL] Expected error not found"
            echo "$output" | grep "\[ERROR\]" | head -1
            FAIL=$((FAIL + 1))
        fi
    else
        if echo "$output" | grep -q "\[ERROR\].*URL"; then
            echo "[FAIL] Unexpected URL validation error"
            echo "$output" | grep "\[ERROR\]" | head -1
            FAIL=$((FAIL + 1))
        else
            echo "[PASS] Validation passed"
            echo "   $(echo "$output" | grep -E "\[INFO\].*TARGET" | head -1)"
            PASS=$((PASS + 1))
        fi
    fi
    echo ""
}

# GEI migrate-repo tests
run_test 1 "migrate-repo: URL in --github-source-org (https)" \
  "dotnet run --project src/gei/gei.csproj -- migrate-repo --github-source-org 'https://github.com/my-org' --source-repo my-repo --github-target-org target-org --github-target-pat dummy" \
  "github-source-org option expects"

run_test 2 "migrate-repo: URL in --source-repo (domain/path)" \
  "dotnet run --project src/gei/gei.csproj -- migrate-repo --github-source-org source-org --source-repo 'github.com/org/my-repo' --github-target-org target-org --github-target-pat dummy" \
  "source-repo option expects"

run_test 3 "migrate-repo: URL in --github-target-org (https)" \
  "dotnet run --project src/gei/gei.csproj -- migrate-repo --github-source-org source-org --source-repo my-repo --github-target-org 'https://github.com/target' --github-target-pat dummy" \
  "github-target-org option expects"

run_test 4 "migrate-repo: URL in --target-repo (http)" \
  "dotnet run --project src/gei/gei.csproj -- migrate-repo --github-source-org source-org --source-repo my-repo --github-target-org target-org --target-repo 'http://github.com/org/repo' --github-target-pat dummy" \
  "target-repo option expects"

run_test 5 "migrate-repo: Valid names (should pass)" \
  "dotnet run --project src/gei/gei.csproj -- migrate-repo --github-source-org source-org --source-repo my-repo --github-target-org target-org --github-target-pat dummy --queue-only" \
  ""

# GEI migrate-org tests
run_test 6 "migrate-org: URL in --github-source-org (www)" \
  "dotnet run --project src/gei/gei.csproj -- migrate-org --github-source-org 'www.github.com/source' --github-target-org target-org --github-target-enterprise my-ent --github-target-pat dummy" \
  "github-source-org option expects"

run_test 7 "migrate-org: URL in --github-target-org" \
  "dotnet run --project src/gei/gei.csproj -- migrate-org --github-source-org source-org --github-target-org 'https://github.com/target' --github-target-enterprise my-ent --github-target-pat dummy" \
  "github-target-org option expects"

run_test 8 "migrate-org: URL in --github-target-enterprise" \
  "dotnet run --project src/gei/gei.csproj -- migrate-org --github-source-org source-org --github-target-org target-org --github-target-enterprise 'https://github.com/enterprises/my-ent' --github-target-pat dummy" \
  "github-target-enterprise option expects"

# GEI generate-script tests
run_test 9 "generate-script: URL in --github-source-org (http)" \
  "dotnet run --project src/gei/gei.csproj -- generate-script --github-source-org 'http://github.com/source' --github-target-org target --output test.ps1" \
  "github-source-org option expects"

run_test 10 "generate-script: URL in --github-target-org" \
  "dotnet run --project src/gei/gei.csproj -- generate-script --github-source-org source --github-target-org 'github.com/target/org' --output test.ps1" \
  "github-target-org option expects"

# ADO2GH migrate-repo tests
run_test 11 "ado2gh migrate-repo: URL in --github-org (www)" \
  "dotnet run --project src/ado2gh/ado2gh.csproj -- migrate-repo --ado-org ado --ado-team-project proj --ado-repo repo --github-org 'www.github.com' --github-repo my-repo --ado-pat dummy --github-pat dummy" \
  "github-org option expects"

run_test 12 "ado2gh migrate-repo: URL in --github-repo" \
  "dotnet run --project src/ado2gh/ado2gh.csproj -- migrate-repo --ado-org ado --ado-team-project proj --ado-repo repo --github-org my-org --github-repo 'https://github.com/org/repo' --ado-pat dummy --github-pat dummy" \
  "github-repo option expects"

# ADO2GH integrate-boards tests
run_test 13 "ado2gh integrate-boards: URL in --github-org" \
  "dotnet run --project src/ado2gh/ado2gh.csproj -- integrate-boards --ado-org ado --ado-team-project proj --github-org 'github.com/org' --github-repo repo --ado-pat dummy --github-pat dummy" \
  "github-org option expects"

run_test 14 "ado2gh integrate-boards: URL in --github-repo" \
  "dotnet run --project src/ado2gh/ado2gh.csproj -- integrate-boards --ado-org ado --ado-team-project proj --github-org org --github-repo 'https://github.com/org/repo' --ado-pat dummy --github-pat dummy" \
  "github-repo option expects"

# GEI migrate-secret-alerts tests
run_test 15 "migrate-secret-alerts: URL in --source-org" \
  "dotnet run --project src/gei/gei.csproj -- migrate-secret-alerts --source-org 'https://github.com/source' --source-repo repo --target-org target --github-target-pat dummy" \
  "source-org option expects"

run_test 16 "migrate-secret-alerts: URL in --target-org" \
  "dotnet run --project src/gei/gei.csproj -- migrate-secret-alerts --source-org source --source-repo repo --target-org 'github.com/target' --github-target-pat dummy" \
  "target-org option expects"

run_test 17 "migrate-secret-alerts: URL in --source-repo" \
  "dotnet run --project src/gei/gei.csproj -- migrate-secret-alerts --source-org source --source-repo 'www.github.com/repo' --target-org target --github-target-pat dummy" \
  "source-repo option expects"

# GEI migrate-code-scanning-alerts tests
run_test 18 "migrate-code-scanning-alerts: URL in --source-org" \
  "dotnet run --project src/gei/gei.csproj -- migrate-code-scanning-alerts --source-org 'http://github.com/src' --source-repo repo --target-org target --github-target-pat dummy" \
  "source-org option expects"

run_test 19 "migrate-code-scanning-alerts: URL in --target-repo" \
  "dotnet run --project src/gei/gei.csproj -- migrate-code-scanning-alerts --source-org source --source-repo repo --target-org target --target-repo 'github.com/target/repo' --github-target-pat dummy" \
  "target-repo option expects"

# GEI download-logs tests
run_test 20 "download-logs: URL in --github-target-org" \
  "dotnet run --project src/gei/gei.csproj -- download-logs --github-target-org 'https://github.com/org' --target-repo repo --github-target-pat dummy" \
  "github-org option expects"

run_test 21 "download-logs: URL in --target-repo" \
  "dotnet run --project src/gei/gei.csproj -- download-logs --github-target-org org --target-repo 'www.github.com/org/repo' --github-target-pat dummy" \
  "github-repo option expects"

# GEI create-team tests
run_test 22 "create-team: URL in --github-org" \
  "dotnet run --project src/gei/gei.csproj -- create-team --github-org 'http://github.com/org' --team-name my-team --github-target-pat dummy" \
  "github-org option expects"

# GEI grant-migrator-role tests
run_test 23 "grant-migrator-role: URL in --github-org" \
  "dotnet run --project src/gei/gei.csproj -- grant-migrator-role --github-org 'github.com/org' --actor user --actor-type USER --github-target-pat dummy" \
  "github-org option expects"

# GEI revoke-migrator-role tests
run_test 24 "revoke-migrator-role: URL in --github-org" \
  "dotnet run --project src/gei/gei.csproj -- revoke-migrator-role --github-org 'https://github.com/my-org' --actor user --actor-type USER --github-target-pat dummy" \
  "github-org option expects"

# GEI generate-mannequin-csv tests
run_test 25 "generate-mannequin-csv: URL in --github-target-org" \
  "dotnet run --project src/gei/gei.csproj -- generate-mannequin-csv --github-target-org 'www.github.com' --output mannequins.csv --github-target-pat dummy" \
  "github-org option expects"

# GEI reclaim-mannequin tests
run_test 26 "reclaim-mannequin: URL in --github-target-org" \
  "dotnet run --project src/gei/gei.csproj -- reclaim-mannequin --github-target-org 'http://github.com/org' --csv mannequins.csv --github-target-pat dummy" \
  "github-org option expects"

# Edge cases - valid names
run_test 27 "Edge: Names with hyphens (valid)" \
  "dotnet run --project src/gei/gei.csproj -- migrate-repo --github-source-org source-org-123 --source-repo my-repo-name --github-target-org target-org-456 --github-target-pat dummy --queue-only" \
  ""

# Test 28: Repo name with underscore — should PASS (repos allow underscores)
run_test 28 "Edge: Repo name with underscore (valid for repos)" \
  "dotnet run --project src/gei/gei.csproj -- migrate-repo --github-source-org source-org --source-repo my_repo --github-target-org target-org --github-target-pat dummy --queue-only" \
  ""

echo "================================================================================"
echo "                           TEST SUMMARY                                         "
echo "================================================================================"
echo ""
echo "Passed: $PASS / 28"
echo "Failed: $FAIL / 28"
echo "--------------------------------------------------------------------------------"
echo ""

if [ $FAIL -eq 0 ]; then
    echo "ALL TESTS PASSED! URL validation working correctly across all commands."
    exit 0
else
    echo "SOME TESTS FAILED. Please review the failures above."
    exit 1
fi
