package main

import (
	"bytes"
	"context"
	"fmt"
	"strings"
	"testing"

	"github.com/github/gh-gei/pkg/ado"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/github/gh-gei/pkg/scriptgen"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

// ---------------------------------------------------------------------------
// Test constants
// ---------------------------------------------------------------------------

const (
	ADO_ORG          = "ADO_ORG"
	ADO_TEAM_PROJECT = "ADO_TEAM_PROJECT"
	FOO_REPO         = "FOO_REPO"
	FOO_PIPELINE     = "FOO_PIPELINE"
	BAR_REPO         = "BAR_REPO"
	BAR_PIPELINE     = "BAR_PIPELINE"
	APP_ID           = "d9edf292-c6fd-4440-af2b-d08fcc9c9dd1"
	GITHUB_ORG       = "GITHUB_ORG"
	ADO_SERVER_URL   = "http://ado.contoso.com"
	testVersion      = "1.1.1"
)

// ---------------------------------------------------------------------------
// Mock implementations
// ---------------------------------------------------------------------------

type mockGenScriptAdoAPI struct {
	getTeamProjectsResult map[string][]string
	getTeamProjectsErr    error

	getGithubAppIdResult map[string]string
	getGithubAppIdErr    error
}

func (m *mockGenScriptAdoAPI) GetTeamProjects(_ context.Context, org string) ([]string, error) {
	if m.getTeamProjectsErr != nil {
		return nil, m.getTeamProjectsErr
	}
	return m.getTeamProjectsResult[org], nil
}

func (m *mockGenScriptAdoAPI) GetGithubAppId(_ context.Context, org, _ string, _ []string) (string, error) {
	if m.getGithubAppIdErr != nil {
		return "", m.getGithubAppIdErr
	}
	return m.getGithubAppIdResult[org], nil
}

type mockGenScriptInspector struct {
	orgs         []string
	teamProjects map[string][]string
	repos        map[string][]ado.Repository // key: "org/teamProject"
	pipelines    map[string][]string         // key: "org/teamProject/repo"
	repoCount    int
	loadedCSV    string
	outputCalled bool
}

func (m *mockGenScriptInspector) GetOrgs(_ context.Context) ([]string, error) {
	return m.orgs, nil
}

func (m *mockGenScriptInspector) GetTeamProjects(_ context.Context, org string) ([]string, error) {
	return m.teamProjects[org], nil
}

func (m *mockGenScriptInspector) GetRepos(_ context.Context, org, teamProject string) ([]ado.Repository, error) {
	key := org + "/" + teamProject
	return m.repos[key], nil
}

func (m *mockGenScriptInspector) GetPipelines(_ context.Context, org, teamProject, repo string) ([]string, error) {
	key := org + "/" + teamProject + "/" + repo
	return m.pipelines[key], nil
}

func (m *mockGenScriptInspector) GetRepoCount(_ context.Context) (int, error) {
	return m.repoCount, nil
}

func (m *mockGenScriptInspector) LoadReposCsv(csvPath string) error {
	m.loadedCSV = csvPath
	return nil
}

func (m *mockGenScriptInspector) OutputRepoListToLog() {
	m.outputCalled = true
}

// ---------------------------------------------------------------------------
// Helper: trimNonExecutableLines
// ---------------------------------------------------------------------------

// trimNonExecutableLines mirrors the C# TrimNonExecutableLines helper.
// It splits on \n, removes empty lines and lines starting with #,
// then skips the first skipFirst and last skipLast of the remaining lines.
func trimNonExecutableLines(script string, skipFirst, skipLast int) string {
	raw := strings.Split(script, "\n")
	var filtered []string
	for _, line := range raw {
		if strings.TrimSpace(line) == "" {
			continue
		}
		if strings.HasPrefix(strings.TrimSpace(line), "#") {
			continue
		}
		filtered = append(filtered, line)
	}

	if skipFirst > len(filtered) {
		skipFirst = len(filtered)
	}
	filtered = filtered[skipFirst:]

	if skipLast > len(filtered) {
		skipLast = len(filtered)
	}
	if skipLast > 0 {
		filtered = filtered[:len(filtered)-skipLast]
	}

	return strings.Join(filtered, "\n")
}

// ---------------------------------------------------------------------------
// Helper: run generate-script command
// ---------------------------------------------------------------------------

func runGenScript(t *testing.T, adoAPI *mockGenScriptAdoAPI, inspector *mockGenScriptInspector, verbose bool, args ...string) string {
	t.Helper()

	oldVersion := version
	version = testVersion
	t.Cleanup(func() { version = oldVersion })

	var buf bytes.Buffer
	log := logger.New(verbose, &buf)

	var scriptOutput string
	writeToFile := func(_, content string) error {
		scriptOutput = content
		return nil
	}

	cmd := newGenerateScriptCmd(adoAPI, inspector, log, writeToFile)
	cmd.SetArgs(args)
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)

	err := cmd.ExecuteContext(context.Background())
	require.NoError(t, err)

	return scriptOutput
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

func TestSequentialScript_StartsWith_Shebang(t *testing.T) {
	inspector := &mockGenScriptInspector{repoCount: 1}
	adoAPI := &mockGenScriptAdoAPI{}

	output := runGenScript(t, adoAPI, inspector, false,
		"--github-org", GITHUB_ORG,
		"--ado-org", ADO_ORG,
		"--sequential",
		"--output", "unit-test-output",
	)

	assert.True(t, strings.HasPrefix(output, "#!/usr/bin/env pwsh"))
}

func TestSequentialScript_Single_Repo_No_Options(t *testing.T) {
	inspector := &mockGenScriptInspector{
		repoCount:    1,
		orgs:         []string{ADO_ORG},
		teamProjects: map[string][]string{ADO_ORG: {ADO_TEAM_PROJECT}},
		repos:        map[string][]ado.Repository{ADO_ORG + "/" + ADO_TEAM_PROJECT: {{Name: FOO_REPO}}},
	}
	adoAPI := &mockGenScriptAdoAPI{}

	output := runGenScript(t, adoAPI, inspector, false,
		"--github-org", GITHUB_ORG,
		"--ado-org", ADO_ORG,
		"--sequential",
		"--output", "unit-test-output",
	)

	trimmed := trimNonExecutableLines(output, 21, 0)
	expected := fmt.Sprintf(
		`Exec { gh ado2gh migrate-repo --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" --github-org "%s" --github-repo "%s-%s" --target-repo-visibility private }`,
		ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO,
	)

	assert.Equal(t, expected, trimmed)
}

func TestSequentialScript_Single_Repo_With_TargetApiUrl(t *testing.T) {
	inspector := &mockGenScriptInspector{
		repoCount:    1,
		orgs:         []string{ADO_ORG},
		teamProjects: map[string][]string{ADO_ORG: {ADO_TEAM_PROJECT}},
		repos:        map[string][]ado.Repository{ADO_ORG + "/" + ADO_TEAM_PROJECT: {{Name: FOO_REPO}}},
	}
	adoAPI := &mockGenScriptAdoAPI{}

	targetAPIURL := "https://foo.com/api/v3"
	output := runGenScript(t, adoAPI, inspector, false,
		"--github-org", GITHUB_ORG,
		"--ado-org", ADO_ORG,
		"--sequential",
		"--output", "unit-test-output",
		"--target-api-url", targetAPIURL,
	)

	trimmed := trimNonExecutableLines(output, 21, 0)
	expected := fmt.Sprintf(
		`Exec { gh ado2gh migrate-repo --target-api-url "%s" --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" --github-org "%s" --github-repo "%s-%s" --target-repo-visibility private }`,
		targetAPIURL, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO,
	)

	assert.Equal(t, expected, trimmed)
}

func TestSequentialScript_Single_Repo_AdoServer(t *testing.T) {
	inspector := &mockGenScriptInspector{
		repoCount:    1,
		orgs:         []string{ADO_ORG},
		teamProjects: map[string][]string{ADO_ORG: {ADO_TEAM_PROJECT}},
		repos:        map[string][]ado.Repository{ADO_ORG + "/" + ADO_TEAM_PROJECT: {{Name: FOO_REPO}}},
	}
	adoAPI := &mockGenScriptAdoAPI{}

	output := runGenScript(t, adoAPI, inspector, false,
		"--github-org", GITHUB_ORG,
		"--ado-org", ADO_ORG,
		"--sequential",
		"--output", "unit-test-output",
		"--ado-server-url", ADO_SERVER_URL,
	)

	trimmed := trimNonExecutableLines(output, 21, 0)
	expected := fmt.Sprintf(
		`Exec { gh ado2gh migrate-repo --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" --github-org "%s" --github-repo "%s-%s" --target-repo-visibility private --ado-server-url "%s" }`,
		ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO, ADO_SERVER_URL,
	)

	assert.Equal(t, expected, trimmed)
}

func TestSequentialScript_With_RepoList(t *testing.T) {
	inspector := &mockGenScriptInspector{
		repoCount:    1,
		orgs:         []string{ADO_ORG},
		teamProjects: map[string][]string{ADO_ORG: {ADO_TEAM_PROJECT}},
		repos:        map[string][]ado.Repository{ADO_ORG + "/" + ADO_TEAM_PROJECT: {{Name: FOO_REPO}}},
	}
	adoAPI := &mockGenScriptAdoAPI{}

	output := runGenScript(t, adoAPI, inspector, false,
		"--github-org", GITHUB_ORG,
		"--ado-org", ADO_ORG,
		"--sequential",
		"--output", "unit-test-output",
		"--repo-list", "repos.csv",
	)

	trimmed := trimNonExecutableLines(output, 21, 0)
	expected := fmt.Sprintf(
		`Exec { gh ado2gh migrate-repo --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" --github-org "%s" --github-repo "%s-%s" --target-repo-visibility private }`,
		ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO,
	)

	assert.Equal(t, expected, trimmed)
	assert.Equal(t, "repos.csv", inspector.loadedCSV)
}

func TestSequentialScript_Single_Repo_All_Options(t *testing.T) {
	inspector := &mockGenScriptInspector{
		repoCount:    1,
		orgs:         []string{ADO_ORG},
		teamProjects: map[string][]string{ADO_ORG: {ADO_TEAM_PROJECT}},
		repos:        map[string][]ado.Repository{ADO_ORG + "/" + ADO_TEAM_PROJECT: {{Name: FOO_REPO}}},
		pipelines:    map[string][]string{ADO_ORG + "/" + ADO_TEAM_PROJECT + "/" + FOO_REPO: {}},
	}
	adoAPI := &mockGenScriptAdoAPI{
		getTeamProjectsResult: map[string][]string{ADO_ORG: {ADO_TEAM_PROJECT}},
	}

	output := runGenScript(t, adoAPI, inspector, false,
		"--github-org", GITHUB_ORG,
		"--ado-org", ADO_ORG,
		"--sequential",
		"--output", "unit-test-output",
		"--all",
	)

	trimmed := trimNonExecutableLines(output, 21, 0)

	lines := []string{
		fmt.Sprintf(`Exec { gh ado2gh create-team --github-org "%s" --team-name "%s-Maintainers" --idp-group "%s-Maintainers" }`, GITHUB_ORG, ADO_TEAM_PROJECT, ADO_TEAM_PROJECT),
		fmt.Sprintf(`Exec { gh ado2gh create-team --github-org "%s" --team-name "%s-Admins" --idp-group "%s-Admins" }`, GITHUB_ORG, ADO_TEAM_PROJECT, ADO_TEAM_PROJECT),
		fmt.Sprintf(`Exec { gh ado2gh lock-ado-repo --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" }`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO),
		fmt.Sprintf(`Exec { gh ado2gh migrate-repo --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" --github-org "%s" --github-repo "%s-%s" --target-repo-visibility private }`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO),
		fmt.Sprintf(`Exec { gh ado2gh disable-ado-repo --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" }`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO),
		fmt.Sprintf(`Exec { gh ado2gh add-team-to-repo --github-org "%s" --github-repo "%s-%s" --team "%s-Maintainers" --role "maintain" }`, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO, ADO_TEAM_PROJECT),
		fmt.Sprintf(`Exec { gh ado2gh add-team-to-repo --github-org "%s" --github-repo "%s-%s" --team "%s-Admins" --role "admin" }`, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO, ADO_TEAM_PROJECT),
		fmt.Sprintf(`Exec { gh ado2gh download-logs --github-org "%s" --github-repo "%s-%s" }`, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO),
	}
	expected := strings.Join(lines, "\n")

	assert.Equal(t, expected, trimmed)
}

func TestReplaces_Invalid_Chars_With_Dashes(t *testing.T) {
	adoTeamProject := "Parts Unlimited"
	cleanedAdoTeamProject := "Parts-Unlimited"
	adoRepo := "Some Repo"
	expectedGithubRepoName := "Parts-Unlimited-Some-Repo"

	inspector := &mockGenScriptInspector{
		repoCount:    1,
		orgs:         []string{ADO_ORG},
		teamProjects: map[string][]string{ADO_ORG: {adoTeamProject}},
		repos:        map[string][]ado.Repository{ADO_ORG + "/" + adoTeamProject: {{Name: adoRepo}}},
		pipelines:    map[string][]string{ADO_ORG + "/" + adoTeamProject + "/" + adoRepo: {}},
	}
	adoAPI := &mockGenScriptAdoAPI{
		getTeamProjectsResult: map[string][]string{ADO_ORG: {adoTeamProject}},
	}

	output := runGenScript(t, adoAPI, inspector, false,
		"--github-org", GITHUB_ORG,
		"--ado-org", ADO_ORG,
		"--sequential",
		"--output", "unit-test-output",
		"--all",
	)

	trimmed := trimNonExecutableLines(output, 21, 0)

	lines := []string{
		fmt.Sprintf(`Exec { gh ado2gh create-team --github-org "%s" --team-name "%s-Maintainers" --idp-group "%s-Maintainers" }`, GITHUB_ORG, cleanedAdoTeamProject, cleanedAdoTeamProject),
		fmt.Sprintf(`Exec { gh ado2gh create-team --github-org "%s" --team-name "%s-Admins" --idp-group "%s-Admins" }`, GITHUB_ORG, cleanedAdoTeamProject, cleanedAdoTeamProject),
		fmt.Sprintf(`Exec { gh ado2gh lock-ado-repo --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" }`, ADO_ORG, adoTeamProject, adoRepo),
		fmt.Sprintf(`Exec { gh ado2gh migrate-repo --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" --github-org "%s" --github-repo "%s" --target-repo-visibility private }`, ADO_ORG, adoTeamProject, adoRepo, GITHUB_ORG, expectedGithubRepoName),
		fmt.Sprintf(`Exec { gh ado2gh disable-ado-repo --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" }`, ADO_ORG, adoTeamProject, adoRepo),
		fmt.Sprintf(`Exec { gh ado2gh add-team-to-repo --github-org "%s" --github-repo "%s" --team "%s-Maintainers" --role "maintain" }`, GITHUB_ORG, expectedGithubRepoName, cleanedAdoTeamProject),
		fmt.Sprintf(`Exec { gh ado2gh add-team-to-repo --github-org "%s" --github-repo "%s" --team "%s-Admins" --role "admin" }`, GITHUB_ORG, expectedGithubRepoName, cleanedAdoTeamProject),
		fmt.Sprintf(`Exec { gh ado2gh download-logs --github-org "%s" --github-repo "%s" }`, GITHUB_ORG, expectedGithubRepoName),
	}
	expected := strings.Join(lines, "\n")

	assert.Equal(t, expected, trimmed)
}

func TestSequentialScript_Single_Repo_No_Options_With_Download_Migration_Logs(t *testing.T) {
	inspector := &mockGenScriptInspector{
		repoCount:    1,
		orgs:         []string{ADO_ORG},
		teamProjects: map[string][]string{ADO_ORG: {ADO_TEAM_PROJECT}},
		repos:        map[string][]ado.Repository{ADO_ORG + "/" + ADO_TEAM_PROJECT: {{Name: FOO_REPO}}},
	}
	adoAPI := &mockGenScriptAdoAPI{}

	output := runGenScript(t, adoAPI, inspector, false,
		"--github-org", GITHUB_ORG,
		"--ado-org", ADO_ORG,
		"--sequential",
		"--output", "unit-test-output",
		"--download-migration-logs",
	)

	trimmed := trimNonExecutableLines(output, 21, 0)

	lines := []string{
		fmt.Sprintf(`Exec { gh ado2gh migrate-repo --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" --github-org "%s" --github-repo "%s-%s" --target-repo-visibility private }`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO),
		fmt.Sprintf(`Exec { gh ado2gh download-logs --github-org "%s" --github-repo "%s-%s" }`, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO),
	}
	expected := strings.Join(lines, "\n")

	assert.Equal(t, expected, trimmed)
}

func TestSequentialScript_Skips_Team_Project_With_No_Repos(t *testing.T) {
	inspector := &mockGenScriptInspector{repoCount: 0}
	adoAPI := &mockGenScriptAdoAPI{}

	var buf bytes.Buffer
	log := logger.New(false, &buf)

	oldVersion := version
	version = testVersion
	defer func() { version = oldVersion }()

	var scriptOutput string
	writeToFile := func(_, content string) error {
		scriptOutput = content
		return nil
	}

	cmd := newGenerateScriptCmd(adoAPI, inspector, log, writeToFile)
	cmd.SetArgs([]string{
		"--github-org", GITHUB_ORG,
		"--ado-org", ADO_ORG,
		"--sequential",
		"--output", "unit-test-output",
	})
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)

	err := cmd.ExecuteContext(context.Background())
	require.NoError(t, err)

	// scriptOutput should be empty (writeToFile not called)
	assert.Empty(t, scriptOutput)
	assert.Contains(t, buf.String(), "no migratable repos were found")
}

func TestSequentialScript_Single_Repo_Two_Pipelines_All_Options(t *testing.T) {
	inspector := &mockGenScriptInspector{
		repoCount:    1,
		orgs:         []string{ADO_ORG},
		teamProjects: map[string][]string{ADO_ORG: {ADO_TEAM_PROJECT}},
		repos:        map[string][]ado.Repository{ADO_ORG + "/" + ADO_TEAM_PROJECT: {{Name: FOO_REPO}}},
		pipelines:    map[string][]string{ADO_ORG + "/" + ADO_TEAM_PROJECT + "/" + FOO_REPO: {FOO_PIPELINE, BAR_PIPELINE}},
	}
	adoAPI := &mockGenScriptAdoAPI{
		getTeamProjectsResult: map[string][]string{ADO_ORG: {ADO_TEAM_PROJECT}},
		getGithubAppIdResult:  map[string]string{ADO_ORG: APP_ID},
	}

	output := runGenScript(t, adoAPI, inspector, false,
		"--github-org", GITHUB_ORG,
		"--ado-org", ADO_ORG,
		"--sequential",
		"--output", "unit-test-output",
		"--all",
	)

	trimmed := trimNonExecutableLines(output, 21, 0)

	lines := []string{
		fmt.Sprintf(`Exec { gh ado2gh create-team --github-org "%s" --team-name "%s-Maintainers" --idp-group "%s-Maintainers" }`, GITHUB_ORG, ADO_TEAM_PROJECT, ADO_TEAM_PROJECT),
		fmt.Sprintf(`Exec { gh ado2gh create-team --github-org "%s" --team-name "%s-Admins" --idp-group "%s-Admins" }`, GITHUB_ORG, ADO_TEAM_PROJECT, ADO_TEAM_PROJECT),
		fmt.Sprintf(`Exec { gh ado2gh share-service-connection --ado-org "%s" --ado-team-project "%s" --service-connection-id "%s" }`, ADO_ORG, ADO_TEAM_PROJECT, APP_ID),
		fmt.Sprintf(`Exec { gh ado2gh lock-ado-repo --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" }`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO),
		fmt.Sprintf(`Exec { gh ado2gh migrate-repo --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" --github-org "%s" --github-repo "%s-%s" --target-repo-visibility private }`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO),
		fmt.Sprintf(`Exec { gh ado2gh disable-ado-repo --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" }`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO),
		fmt.Sprintf(`Exec { gh ado2gh add-team-to-repo --github-org "%s" --github-repo "%s-%s" --team "%s-Maintainers" --role "maintain" }`, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO, ADO_TEAM_PROJECT),
		fmt.Sprintf(`Exec { gh ado2gh add-team-to-repo --github-org "%s" --github-repo "%s-%s" --team "%s-Admins" --role "admin" }`, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO, ADO_TEAM_PROJECT),
		fmt.Sprintf(`Exec { gh ado2gh download-logs --github-org "%s" --github-repo "%s-%s" }`, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO),
		fmt.Sprintf(`Exec { gh ado2gh rewire-pipeline --ado-org "%s" --ado-team-project "%s" --ado-pipeline "%s" --github-org "%s" --github-repo "%s-%s" --service-connection-id "%s" }`, ADO_ORG, ADO_TEAM_PROJECT, FOO_PIPELINE, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO, APP_ID),
		fmt.Sprintf(`Exec { gh ado2gh rewire-pipeline --ado-org "%s" --ado-team-project "%s" --ado-pipeline "%s" --github-org "%s" --github-repo "%s-%s" --service-connection-id "%s" }`, ADO_ORG, ADO_TEAM_PROJECT, BAR_PIPELINE, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO, APP_ID),
	}
	expected := strings.Join(lines, "\n")

	assert.Equal(t, expected, trimmed)
}

func TestSequentialScript_Single_Repo_Two_Pipelines_No_Service_Connection_All_Options(t *testing.T) {
	inspector := &mockGenScriptInspector{
		repoCount:    1,
		orgs:         []string{ADO_ORG},
		teamProjects: map[string][]string{ADO_ORG: {ADO_TEAM_PROJECT}},
		repos:        map[string][]ado.Repository{ADO_ORG + "/" + ADO_TEAM_PROJECT: {{Name: FOO_REPO}}},
		pipelines:    map[string][]string{ADO_ORG + "/" + ADO_TEAM_PROJECT + "/" + FOO_REPO: {FOO_PIPELINE, BAR_PIPELINE}},
	}
	// No app ID returned => no service connection
	adoAPI := &mockGenScriptAdoAPI{
		getTeamProjectsResult: map[string][]string{ADO_ORG: {ADO_TEAM_PROJECT}},
		getGithubAppIdResult:  map[string]string{},
	}

	output := runGenScript(t, adoAPI, inspector, false,
		"--github-org", GITHUB_ORG,
		"--ado-org", ADO_ORG,
		"--sequential",
		"--output", "unit-test-output",
		"--all",
	)

	trimmed := trimNonExecutableLines(output, 21, 0)

	lines := []string{
		fmt.Sprintf(`Exec { gh ado2gh create-team --github-org "%s" --team-name "%s-Maintainers" --idp-group "%s-Maintainers" }`, GITHUB_ORG, ADO_TEAM_PROJECT, ADO_TEAM_PROJECT),
		fmt.Sprintf(`Exec { gh ado2gh create-team --github-org "%s" --team-name "%s-Admins" --idp-group "%s-Admins" }`, GITHUB_ORG, ADO_TEAM_PROJECT, ADO_TEAM_PROJECT),
		fmt.Sprintf(`Exec { gh ado2gh lock-ado-repo --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" }`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO),
		fmt.Sprintf(`Exec { gh ado2gh migrate-repo --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" --github-org "%s" --github-repo "%s-%s" --target-repo-visibility private }`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO),
		fmt.Sprintf(`Exec { gh ado2gh disable-ado-repo --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" }`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO),
		fmt.Sprintf(`Exec { gh ado2gh add-team-to-repo --github-org "%s" --github-repo "%s-%s" --team "%s-Maintainers" --role "maintain" }`, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO, ADO_TEAM_PROJECT),
		fmt.Sprintf(`Exec { gh ado2gh add-team-to-repo --github-org "%s" --github-repo "%s-%s" --team "%s-Admins" --role "admin" }`, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO, ADO_TEAM_PROJECT),
		fmt.Sprintf(`Exec { gh ado2gh download-logs --github-org "%s" --github-repo "%s-%s" }`, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO),
	}
	expected := strings.Join(lines, "\n")

	assert.Equal(t, expected, trimmed)
}

func TestSequentialScript_Create_Teams_Option(t *testing.T) {
	inspector := &mockGenScriptInspector{
		repoCount:    1,
		orgs:         []string{ADO_ORG},
		teamProjects: map[string][]string{ADO_ORG: {ADO_TEAM_PROJECT}},
		repos:        map[string][]ado.Repository{ADO_ORG + "/" + ADO_TEAM_PROJECT: {{Name: FOO_REPO}}},
	}
	adoAPI := &mockGenScriptAdoAPI{}

	output := runGenScript(t, adoAPI, inspector, false,
		"--github-org", GITHUB_ORG,
		"--ado-org", ADO_ORG,
		"--sequential",
		"--output", "unit-test-output",
		"--create-teams",
	)

	trimmed := trimNonExecutableLines(output, 21, 0)

	lines := []string{
		fmt.Sprintf(`Exec { gh ado2gh create-team --github-org "%s" --team-name "%s-Maintainers" }`, GITHUB_ORG, ADO_TEAM_PROJECT),
		fmt.Sprintf(`Exec { gh ado2gh create-team --github-org "%s" --team-name "%s-Admins" }`, GITHUB_ORG, ADO_TEAM_PROJECT),
		fmt.Sprintf(`Exec { gh ado2gh migrate-repo --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" --github-org "%s" --github-repo "%s-%s" --target-repo-visibility private }`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO),
		fmt.Sprintf(`Exec { gh ado2gh add-team-to-repo --github-org "%s" --github-repo "%s-%s" --team "%s-Maintainers" --role "maintain" }`, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO, ADO_TEAM_PROJECT),
		fmt.Sprintf(`Exec { gh ado2gh add-team-to-repo --github-org "%s" --github-repo "%s-%s" --team "%s-Admins" --role "admin" }`, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO, ADO_TEAM_PROJECT),
	}
	expected := strings.Join(lines, "\n")

	assert.Equal(t, expected, trimmed)
}

func TestSequentialScript_Link_Idp_Groups_Option(t *testing.T) {
	inspector := &mockGenScriptInspector{
		repoCount:    1,
		orgs:         []string{ADO_ORG},
		teamProjects: map[string][]string{ADO_ORG: {ADO_TEAM_PROJECT}},
		repos:        map[string][]ado.Repository{ADO_ORG + "/" + ADO_TEAM_PROJECT: {{Name: FOO_REPO}}},
	}
	adoAPI := &mockGenScriptAdoAPI{}

	output := runGenScript(t, adoAPI, inspector, false,
		"--github-org", GITHUB_ORG,
		"--ado-org", ADO_ORG,
		"--sequential",
		"--output", "unit-test-output",
		"--link-idp-groups",
	)

	trimmed := trimNonExecutableLines(output, 21, 0)

	lines := []string{
		fmt.Sprintf(`Exec { gh ado2gh create-team --github-org "%s" --team-name "%s-Maintainers" --idp-group "%s-Maintainers" }`, GITHUB_ORG, ADO_TEAM_PROJECT, ADO_TEAM_PROJECT),
		fmt.Sprintf(`Exec { gh ado2gh create-team --github-org "%s" --team-name "%s-Admins" --idp-group "%s-Admins" }`, GITHUB_ORG, ADO_TEAM_PROJECT, ADO_TEAM_PROJECT),
		fmt.Sprintf(`Exec { gh ado2gh migrate-repo --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" --github-org "%s" --github-repo "%s-%s" --target-repo-visibility private }`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO),
		fmt.Sprintf(`Exec { gh ado2gh add-team-to-repo --github-org "%s" --github-repo "%s-%s" --team "%s-Maintainers" --role "maintain" }`, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO, ADO_TEAM_PROJECT),
		fmt.Sprintf(`Exec { gh ado2gh add-team-to-repo --github-org "%s" --github-repo "%s-%s" --team "%s-Admins" --role "admin" }`, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO, ADO_TEAM_PROJECT),
	}
	expected := strings.Join(lines, "\n")

	assert.Equal(t, expected, trimmed)
}

func TestSequentialScript_Lock_Ado_Repo_Option(t *testing.T) {
	inspector := &mockGenScriptInspector{
		repoCount:    1,
		orgs:         []string{ADO_ORG},
		teamProjects: map[string][]string{ADO_ORG: {ADO_TEAM_PROJECT}},
		repos:        map[string][]ado.Repository{ADO_ORG + "/" + ADO_TEAM_PROJECT: {{Name: FOO_REPO}}},
	}
	adoAPI := &mockGenScriptAdoAPI{}

	output := runGenScript(t, adoAPI, inspector, false,
		"--github-org", GITHUB_ORG,
		"--ado-org", ADO_ORG,
		"--sequential",
		"--output", "unit-test-output",
		"--lock-ado-repos",
	)

	trimmed := trimNonExecutableLines(output, 21, 0)

	lines := []string{
		fmt.Sprintf(`Exec { gh ado2gh lock-ado-repo --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" }`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO),
		fmt.Sprintf(`Exec { gh ado2gh migrate-repo --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" --github-org "%s" --github-repo "%s-%s" --target-repo-visibility private }`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO),
	}
	expected := strings.Join(lines, "\n")

	assert.Equal(t, expected, trimmed)
}

func TestSequentialScript_Disable_Ado_Repo_Option(t *testing.T) {
	inspector := &mockGenScriptInspector{
		repoCount:    1,
		orgs:         []string{ADO_ORG},
		teamProjects: map[string][]string{ADO_ORG: {ADO_TEAM_PROJECT}},
		repos:        map[string][]ado.Repository{ADO_ORG + "/" + ADO_TEAM_PROJECT: {{Name: FOO_REPO}}},
	}
	adoAPI := &mockGenScriptAdoAPI{}

	output := runGenScript(t, adoAPI, inspector, false,
		"--github-org", GITHUB_ORG,
		"--ado-org", ADO_ORG,
		"--sequential",
		"--output", "unit-test-output",
		"--disable-ado-repos",
	)

	trimmed := trimNonExecutableLines(output, 21, 0)

	lines := []string{
		fmt.Sprintf(`Exec { gh ado2gh migrate-repo --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" --github-org "%s" --github-repo "%s-%s" --target-repo-visibility private }`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO),
		fmt.Sprintf(`Exec { gh ado2gh disable-ado-repo --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" }`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO),
	}
	expected := strings.Join(lines, "\n")

	assert.Contains(t, trimmed, expected)
}

func TestSequentialScript_Rewire_Pipelines_Option(t *testing.T) {
	inspector := &mockGenScriptInspector{
		repoCount:    1,
		orgs:         []string{ADO_ORG},
		teamProjects: map[string][]string{ADO_ORG: {ADO_TEAM_PROJECT}},
		repos:        map[string][]ado.Repository{ADO_ORG + "/" + ADO_TEAM_PROJECT: {{Name: FOO_REPO}}},
		pipelines:    map[string][]string{ADO_ORG + "/" + ADO_TEAM_PROJECT + "/" + FOO_REPO: {FOO_PIPELINE}},
	}
	adoAPI := &mockGenScriptAdoAPI{
		getTeamProjectsResult: map[string][]string{ADO_ORG: {ADO_TEAM_PROJECT}},
		getGithubAppIdResult:  map[string]string{ADO_ORG: APP_ID},
	}

	output := runGenScript(t, adoAPI, inspector, false,
		"--github-org", GITHUB_ORG,
		"--ado-org", ADO_ORG,
		"--sequential",
		"--output", "unit-test-output",
		"--rewire-pipelines",
	)

	trimmed := trimNonExecutableLines(output, 21, 0)

	lines := []string{
		fmt.Sprintf(`Exec { gh ado2gh share-service-connection --ado-org "%s" --ado-team-project "%s" --service-connection-id "%s" }`, ADO_ORG, ADO_TEAM_PROJECT, APP_ID),
		fmt.Sprintf(`Exec { gh ado2gh migrate-repo --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" --github-org "%s" --github-repo "%s-%s" --target-repo-visibility private }`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO),
		fmt.Sprintf(`Exec { gh ado2gh rewire-pipeline --ado-org "%s" --ado-team-project "%s" --ado-pipeline "%s" --github-org "%s" --github-repo "%s-%s" --service-connection-id "%s" }`, ADO_ORG, ADO_TEAM_PROJECT, FOO_PIPELINE, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO, APP_ID),
	}
	expected := strings.Join(lines, "\n")

	assert.Contains(t, trimmed, expected)
}

// ---------------------------------------------------------------------------
// Parallel tests
// ---------------------------------------------------------------------------

func TestParallelScript_StartsWith_Shebang(t *testing.T) {
	inspector := &mockGenScriptInspector{repoCount: 1}
	adoAPI := &mockGenScriptAdoAPI{}

	output := runGenScript(t, adoAPI, inspector, false,
		"--github-org", GITHUB_ORG,
		"--ado-org", ADO_ORG,
		"--output", "unit-test-output",
	)

	assert.True(t, strings.HasPrefix(output, "#!/usr/bin/env pwsh"))
}

func TestParallelScript_Single_Repo_No_Options(t *testing.T) {
	inspector := &mockGenScriptInspector{
		repoCount:    1,
		orgs:         []string{ADO_ORG},
		teamProjects: map[string][]string{ADO_ORG: {ADO_TEAM_PROJECT}},
		repos:        map[string][]ado.Repository{ADO_ORG + "/" + ADO_TEAM_PROJECT: {{Name: FOO_REPO}}},
	}
	adoAPI := &mockGenScriptAdoAPI{}

	output := runGenScript(t, adoAPI, inspector, false,
		"--github-org", GITHUB_ORG,
		"--ado-org", ADO_ORG,
		"--output", "unit-test-output",
	)

	// Full script comparison — build expected from C# test
	var sb strings.Builder
	sb.WriteString("#!/usr/bin/env pwsh\n")
	sb.WriteString("\n")
	sb.WriteString("# =========== Created with CLI version 1.1.1 ===========\n")
	sb.WriteString(scriptgen.ExecFunctionBlock + "\n")
	sb.WriteString(scriptgen.ExecAndGetMigrationIDFunctionBlock + "\n")
	sb.WriteString(scriptgen.ExecBatchFunctionBlock + "\n")
	sb.WriteString(scriptgen.ValidateADOEnvVars + "\n")
	sb.WriteString("\n")
	sb.WriteString("$Succeeded = 0\n")
	sb.WriteString("$Failed = 0\n")
	sb.WriteString("$RepoMigrations = [ordered]@{}\n")
	sb.WriteString("\n")
	fmt.Fprintf(&sb, "# =========== Queueing migration for Organization: %s ===========\n", ADO_ORG)
	sb.WriteString("\n")
	fmt.Fprintf(&sb, "# === Queueing repo migrations for Team Project: %s/%s ===\n", ADO_ORG, ADO_TEAM_PROJECT)
	sb.WriteString("\n")
	sb.WriteString(fmt.Sprintf(`$MigrationID = ExecAndGetMigrationID { gh ado2gh migrate-repo --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" --github-org "%s" --github-repo "%s-%s" --queue-only --target-repo-visibility private }`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO) + "\n")
	sb.WriteString(fmt.Sprintf(`$RepoMigrations["%s/%s-%s"] = $MigrationID`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO) + "\n")
	sb.WriteString("\n")
	fmt.Fprintf(&sb, "# =========== Waiting for all migrations to finish for Organization: %s ===========\n", ADO_ORG)
	sb.WriteString("\n")
	fmt.Fprintf(&sb, "# === Waiting for repo migration to finish for Team Project: %s and Repo: %s. Will then complete the below post migration steps. ===\n", ADO_TEAM_PROJECT, FOO_REPO)
	sb.WriteString("$CanExecuteBatch = $false\n")
	sb.WriteString(fmt.Sprintf(`if ($null -ne $RepoMigrations["%s/%s-%s"]) {`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO) + "\n")
	sb.WriteString(fmt.Sprintf(`    gh ado2gh wait-for-migration --migration-id $RepoMigrations["%s/%s-%s"]`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO) + "\n")
	sb.WriteString("    $CanExecuteBatch = ($lastexitcode -eq 0)\n")
	sb.WriteString("}\n")
	sb.WriteString("if ($CanExecuteBatch) {\n")
	sb.WriteString("    $Succeeded++\n")
	sb.WriteString("} else {\n")
	sb.WriteString("    $Failed++\n")
	sb.WriteString("}\n")
	sb.WriteString("\n")
	sb.WriteString("Write-Host =============== Summary ===============\n")
	sb.WriteString("Write-Host Total number of successful migrations: $Succeeded\n")
	sb.WriteString("Write-Host Total number of failed migrations: $Failed\n")
	sb.WriteString("\nif ($Failed -ne 0) {\n    exit 1\n}\n")
	sb.WriteString("\n")
	sb.WriteString("\n")

	assert.Equal(t, sb.String(), output)
}

func TestParallelScript_Single_Repo_No_Options_With_Download_Migration_Logs(t *testing.T) {
	inspector := &mockGenScriptInspector{
		repoCount:    1,
		orgs:         []string{ADO_ORG},
		teamProjects: map[string][]string{ADO_ORG: {ADO_TEAM_PROJECT}},
		repos:        map[string][]ado.Repository{ADO_ORG + "/" + ADO_TEAM_PROJECT: {{Name: FOO_REPO}}},
	}
	adoAPI := &mockGenScriptAdoAPI{}

	output := runGenScript(t, adoAPI, inspector, false,
		"--github-org", GITHUB_ORG,
		"--ado-org", ADO_ORG,
		"--output", "unit-test-output",
		"--download-migration-logs",
	)

	var sb strings.Builder
	sb.WriteString("#!/usr/bin/env pwsh\n")
	sb.WriteString("\n")
	sb.WriteString("# =========== Created with CLI version 1.1.1 ===========\n")
	sb.WriteString(scriptgen.ExecFunctionBlock + "\n")
	sb.WriteString(scriptgen.ExecAndGetMigrationIDFunctionBlock + "\n")
	sb.WriteString(scriptgen.ExecBatchFunctionBlock + "\n")
	sb.WriteString(scriptgen.ValidateADOEnvVars + "\n")
	sb.WriteString("\n")
	sb.WriteString("$Succeeded = 0\n")
	sb.WriteString("$Failed = 0\n")
	sb.WriteString("$RepoMigrations = [ordered]@{}\n")
	sb.WriteString("\n")
	fmt.Fprintf(&sb, "# =========== Queueing migration for Organization: %s ===========\n", ADO_ORG)
	sb.WriteString("\n")
	fmt.Fprintf(&sb, "# === Queueing repo migrations for Team Project: %s/%s ===\n", ADO_ORG, ADO_TEAM_PROJECT)
	sb.WriteString("\n")
	sb.WriteString(fmt.Sprintf(`$MigrationID = ExecAndGetMigrationID { gh ado2gh migrate-repo --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" --github-org "%s" --github-repo "%s-%s" --queue-only --target-repo-visibility private }`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO) + "\n")
	sb.WriteString(fmt.Sprintf(`$RepoMigrations["%s/%s-%s"] = $MigrationID`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO) + "\n")
	sb.WriteString("\n")
	fmt.Fprintf(&sb, "# =========== Waiting for all migrations to finish for Organization: %s ===========\n", ADO_ORG)
	sb.WriteString("\n")
	fmt.Fprintf(&sb, "# === Waiting for repo migration to finish for Team Project: %s and Repo: %s. Will then complete the below post migration steps. ===\n", ADO_TEAM_PROJECT, FOO_REPO)
	sb.WriteString("$CanExecuteBatch = $false\n")
	sb.WriteString(fmt.Sprintf(`if ($null -ne $RepoMigrations["%s/%s-%s"]) {`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO) + "\n")
	sb.WriteString(fmt.Sprintf(`    gh ado2gh wait-for-migration --migration-id $RepoMigrations["%s/%s-%s"]`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO) + "\n")
	sb.WriteString("    $CanExecuteBatch = ($lastexitcode -eq 0)\n")
	sb.WriteString("}\n")
	sb.WriteString("if ($CanExecuteBatch) {\n")
	sb.WriteString("    ExecBatch @(\n")
	sb.WriteString(fmt.Sprintf(`        { gh ado2gh download-logs --github-org "%s" --github-repo "%s-%s" }`, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO) + "\n")
	sb.WriteString("    )\n")
	sb.WriteString("    if ($Global:LastBatchFailures -eq 0) { $Succeeded++ }\n")
	sb.WriteString("} else {\n")
	sb.WriteString("    $Failed++\n")
	sb.WriteString("}\n")
	sb.WriteString("\n")
	sb.WriteString("Write-Host =============== Summary ===============\n")
	sb.WriteString("Write-Host Total number of successful migrations: $Succeeded\n")
	sb.WriteString("Write-Host Total number of failed migrations: $Failed\n")
	sb.WriteString("\nif ($Failed -ne 0) {\n    exit 1\n}\n")
	sb.WriteString("\n")
	sb.WriteString("\n")

	assert.Equal(t, sb.String(), output)
}

func TestParallelScript_Skips_Team_Project_With_No_Repos(t *testing.T) {
	inspector := &mockGenScriptInspector{repoCount: 0}
	adoAPI := &mockGenScriptAdoAPI{}

	var buf bytes.Buffer
	log := logger.New(false, &buf)

	oldVersion := version
	version = testVersion
	defer func() { version = oldVersion }()

	var scriptOutput string
	writeToFile := func(_, content string) error {
		scriptOutput = content
		return nil
	}

	cmd := newGenerateScriptCmd(adoAPI, inspector, log, writeToFile)
	cmd.SetArgs([]string{
		"--github-org", GITHUB_ORG,
		"--ado-org", ADO_ORG,
		"--output", "unit-test-output",
	})
	cmd.SetOut(&buf)
	cmd.SetErr(&buf)

	err := cmd.ExecuteContext(context.Background())
	require.NoError(t, err)

	assert.Empty(t, scriptOutput)
	assert.Contains(t, buf.String(), "no migratable repos were found")
}

func TestParallelScript_Two_Repos_Two_Pipelines_All_Options(t *testing.T) {
	inspector := &mockGenScriptInspector{
		repoCount:    2,
		orgs:         []string{ADO_ORG},
		teamProjects: map[string][]string{ADO_ORG: {ADO_TEAM_PROJECT}},
		repos:        map[string][]ado.Repository{ADO_ORG + "/" + ADO_TEAM_PROJECT: {{Name: FOO_REPO}, {Name: BAR_REPO}}},
		pipelines: map[string][]string{
			ADO_ORG + "/" + ADO_TEAM_PROJECT + "/" + FOO_REPO: {FOO_PIPELINE},
			ADO_ORG + "/" + ADO_TEAM_PROJECT + "/" + BAR_REPO: {BAR_PIPELINE},
		},
	}
	adoAPI := &mockGenScriptAdoAPI{
		getTeamProjectsResult: map[string][]string{ADO_ORG: {ADO_TEAM_PROJECT}},
		getGithubAppIdResult:  map[string]string{ADO_ORG: APP_ID},
	}

	output := runGenScript(t, adoAPI, inspector, false,
		"--github-org", GITHUB_ORG,
		"--ado-org", ADO_ORG,
		"--output", "unit-test-output",
		"--all",
	)

	// Full script comparison
	var sb strings.Builder
	sb.WriteString("#!/usr/bin/env pwsh\n")
	sb.WriteString("\n")
	sb.WriteString("# =========== Created with CLI version 1.1.1 ===========\n")
	sb.WriteString(scriptgen.ExecFunctionBlock + "\n")
	sb.WriteString(scriptgen.ExecAndGetMigrationIDFunctionBlock + "\n")
	sb.WriteString(scriptgen.ExecBatchFunctionBlock + "\n")
	sb.WriteString(scriptgen.ValidateADOEnvVars + "\n")
	sb.WriteString("\n")
	sb.WriteString("$Succeeded = 0\n")
	sb.WriteString("$Failed = 0\n")
	sb.WriteString("$RepoMigrations = [ordered]@{}\n")
	sb.WriteString("\n")
	fmt.Fprintf(&sb, "# =========== Queueing migration for Organization: %s ===========\n", ADO_ORG)
	sb.WriteString("\n")
	fmt.Fprintf(&sb, "# === Queueing repo migrations for Team Project: %s/%s ===\n", ADO_ORG, ADO_TEAM_PROJECT)
	sb.WriteString(fmt.Sprintf(`Exec { gh ado2gh create-team --github-org "%s" --team-name "%s-Maintainers" --idp-group "%s-Maintainers" }`, GITHUB_ORG, ADO_TEAM_PROJECT, ADO_TEAM_PROJECT) + "\n")
	sb.WriteString(fmt.Sprintf(`Exec { gh ado2gh create-team --github-org "%s" --team-name "%s-Admins" --idp-group "%s-Admins" }`, GITHUB_ORG, ADO_TEAM_PROJECT, ADO_TEAM_PROJECT) + "\n")
	sb.WriteString(fmt.Sprintf(`Exec { gh ado2gh share-service-connection --ado-org "%s" --ado-team-project "%s" --service-connection-id "%s" }`, ADO_ORG, ADO_TEAM_PROJECT, APP_ID) + "\n")
	sb.WriteString("\n")
	sb.WriteString(fmt.Sprintf(`Exec { gh ado2gh lock-ado-repo --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" }`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO) + "\n")
	sb.WriteString(fmt.Sprintf(`$MigrationID = ExecAndGetMigrationID { gh ado2gh migrate-repo --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" --github-org "%s" --github-repo "%s-%s" --queue-only --target-repo-visibility private }`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO) + "\n")
	sb.WriteString(fmt.Sprintf(`$RepoMigrations["%s/%s-%s"] = $MigrationID`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO) + "\n")
	sb.WriteString("\n")
	sb.WriteString(fmt.Sprintf(`Exec { gh ado2gh lock-ado-repo --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" }`, ADO_ORG, ADO_TEAM_PROJECT, BAR_REPO) + "\n")
	sb.WriteString(fmt.Sprintf(`$MigrationID = ExecAndGetMigrationID { gh ado2gh migrate-repo --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" --github-org "%s" --github-repo "%s-%s" --queue-only --target-repo-visibility private }`, ADO_ORG, ADO_TEAM_PROJECT, BAR_REPO, GITHUB_ORG, ADO_TEAM_PROJECT, BAR_REPO) + "\n")
	sb.WriteString(fmt.Sprintf(`$RepoMigrations["%s/%s-%s"] = $MigrationID`, ADO_ORG, ADO_TEAM_PROJECT, BAR_REPO) + "\n")
	sb.WriteString("\n")
	fmt.Fprintf(&sb, "# =========== Waiting for all migrations to finish for Organization: %s ===========\n", ADO_ORG)
	sb.WriteString("\n")
	// FOO_REPO waiting
	fmt.Fprintf(&sb, "# === Waiting for repo migration to finish for Team Project: %s and Repo: %s. Will then complete the below post migration steps. ===\n", ADO_TEAM_PROJECT, FOO_REPO)
	sb.WriteString("$CanExecuteBatch = $false\n")
	sb.WriteString(fmt.Sprintf(`if ($null -ne $RepoMigrations["%s/%s-%s"]) {`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO) + "\n")
	sb.WriteString(fmt.Sprintf(`    gh ado2gh wait-for-migration --migration-id $RepoMigrations["%s/%s-%s"]`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO) + "\n")
	sb.WriteString("    $CanExecuteBatch = ($lastexitcode -eq 0)\n")
	sb.WriteString("}\n")
	sb.WriteString("if ($CanExecuteBatch) {\n")
	sb.WriteString("    ExecBatch @(\n")
	sb.WriteString(fmt.Sprintf(`        { gh ado2gh disable-ado-repo --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" }`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO) + "\n")
	sb.WriteString(fmt.Sprintf(`        { gh ado2gh add-team-to-repo --github-org "%s" --github-repo "%s-%s" --team "%s-Maintainers" --role "maintain" }`, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO, ADO_TEAM_PROJECT) + "\n")
	sb.WriteString(fmt.Sprintf(`        { gh ado2gh add-team-to-repo --github-org "%s" --github-repo "%s-%s" --team "%s-Admins" --role "admin" }`, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO, ADO_TEAM_PROJECT) + "\n")
	sb.WriteString(fmt.Sprintf(`        { gh ado2gh download-logs --github-org "%s" --github-repo "%s-%s" }`, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO) + "\n")
	sb.WriteString(fmt.Sprintf(`        { gh ado2gh rewire-pipeline --ado-org "%s" --ado-team-project "%s" --ado-pipeline "%s" --github-org "%s" --github-repo "%s-%s" --service-connection-id "%s" }`, ADO_ORG, ADO_TEAM_PROJECT, FOO_PIPELINE, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO, APP_ID) + "\n")
	sb.WriteString("    )\n")
	sb.WriteString("    if ($Global:LastBatchFailures -eq 0) { $Succeeded++ }\n")
	sb.WriteString("} else {\n")
	sb.WriteString("    $Failed++\n")
	sb.WriteString("}\n")
	sb.WriteString("\n")
	// BAR_REPO waiting
	fmt.Fprintf(&sb, "# === Waiting for repo migration to finish for Team Project: %s and Repo: %s. Will then complete the below post migration steps. ===\n", ADO_TEAM_PROJECT, BAR_REPO)
	sb.WriteString("$CanExecuteBatch = $false\n")
	sb.WriteString(fmt.Sprintf(`if ($null -ne $RepoMigrations["%s/%s-%s"]) {`, ADO_ORG, ADO_TEAM_PROJECT, BAR_REPO) + "\n")
	sb.WriteString(fmt.Sprintf(`    gh ado2gh wait-for-migration --migration-id $RepoMigrations["%s/%s-%s"]`, ADO_ORG, ADO_TEAM_PROJECT, BAR_REPO) + "\n")
	sb.WriteString("    $CanExecuteBatch = ($lastexitcode -eq 0)\n")
	sb.WriteString("}\n")
	sb.WriteString("if ($CanExecuteBatch) {\n")
	sb.WriteString("    ExecBatch @(\n")
	sb.WriteString(fmt.Sprintf(`        { gh ado2gh disable-ado-repo --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" }`, ADO_ORG, ADO_TEAM_PROJECT, BAR_REPO) + "\n")
	sb.WriteString(fmt.Sprintf(`        { gh ado2gh add-team-to-repo --github-org "%s" --github-repo "%s-%s" --team "%s-Maintainers" --role "maintain" }`, GITHUB_ORG, ADO_TEAM_PROJECT, BAR_REPO, ADO_TEAM_PROJECT) + "\n")
	sb.WriteString(fmt.Sprintf(`        { gh ado2gh add-team-to-repo --github-org "%s" --github-repo "%s-%s" --team "%s-Admins" --role "admin" }`, GITHUB_ORG, ADO_TEAM_PROJECT, BAR_REPO, ADO_TEAM_PROJECT) + "\n")
	sb.WriteString(fmt.Sprintf(`        { gh ado2gh download-logs --github-org "%s" --github-repo "%s-%s" }`, GITHUB_ORG, ADO_TEAM_PROJECT, BAR_REPO) + "\n")
	sb.WriteString(fmt.Sprintf(`        { gh ado2gh rewire-pipeline --ado-org "%s" --ado-team-project "%s" --ado-pipeline "%s" --github-org "%s" --github-repo "%s-%s" --service-connection-id "%s" }`, ADO_ORG, ADO_TEAM_PROJECT, BAR_PIPELINE, GITHUB_ORG, ADO_TEAM_PROJECT, BAR_REPO, APP_ID) + "\n")
	sb.WriteString("    )\n")
	sb.WriteString("    if ($Global:LastBatchFailures -eq 0) { $Succeeded++ }\n")
	sb.WriteString("} else {\n")
	sb.WriteString("    $Failed++\n")
	sb.WriteString("}\n")
	sb.WriteString("\n")
	sb.WriteString("Write-Host =============== Summary ===============\n")
	sb.WriteString("Write-Host Total number of successful migrations: $Succeeded\n")
	sb.WriteString("Write-Host Total number of failed migrations: $Failed\n")
	sb.WriteString("\nif ($Failed -ne 0) {\n    exit 1\n}\n")
	sb.WriteString("\n")
	sb.WriteString("\n")

	assert.Equal(t, sb.String(), output)
}

func TestParallelScript_Single_Repo_No_Service_Connection_All_Options(t *testing.T) {
	inspector := &mockGenScriptInspector{
		repoCount:    1,
		orgs:         []string{ADO_ORG},
		teamProjects: map[string][]string{ADO_ORG: {ADO_TEAM_PROJECT}},
		repos:        map[string][]ado.Repository{ADO_ORG + "/" + ADO_TEAM_PROJECT: {{Name: FOO_REPO}}},
		pipelines:    map[string][]string{ADO_ORG + "/" + ADO_TEAM_PROJECT + "/" + FOO_REPO: {FOO_PIPELINE, BAR_PIPELINE}},
	}
	// GetGithubAppId returns empty for this org
	adoAPI := &mockGenScriptAdoAPI{
		getTeamProjectsResult: map[string][]string{ADO_ORG: {ADO_TEAM_PROJECT}},
		getGithubAppIdResult:  map[string]string{},
	}

	output := runGenScript(t, adoAPI, inspector, false,
		"--github-org", GITHUB_ORG,
		"--ado-org", ADO_ORG,
		"--output", "unit-test-output",
		"--all",
	)

	// Full script comparison
	var sb strings.Builder
	sb.WriteString("#!/usr/bin/env pwsh\n")
	sb.WriteString("\n")
	sb.WriteString("# =========== Created with CLI version 1.1.1 ===========\n")
	sb.WriteString(scriptgen.ExecFunctionBlock + "\n")
	sb.WriteString(scriptgen.ExecAndGetMigrationIDFunctionBlock + "\n")
	sb.WriteString(scriptgen.ExecBatchFunctionBlock + "\n")
	sb.WriteString(scriptgen.ValidateADOEnvVars + "\n")
	sb.WriteString("\n")
	sb.WriteString("$Succeeded = 0\n")
	sb.WriteString("$Failed = 0\n")
	sb.WriteString("$RepoMigrations = [ordered]@{}\n")
	sb.WriteString("\n")
	fmt.Fprintf(&sb, "# =========== Queueing migration for Organization: %s ===========\n", ADO_ORG)
	sb.WriteString("\n")
	sb.WriteString("# No GitHub App in this org, skipping the re-wiring of Azure Pipelines to GitHub repos\n")
	sb.WriteString("\n")
	fmt.Fprintf(&sb, "# === Queueing repo migrations for Team Project: %s/%s ===\n", ADO_ORG, ADO_TEAM_PROJECT)
	sb.WriteString(fmt.Sprintf(`Exec { gh ado2gh create-team --github-org "%s" --team-name "%s-Maintainers" --idp-group "%s-Maintainers" }`, GITHUB_ORG, ADO_TEAM_PROJECT, ADO_TEAM_PROJECT) + "\n")
	sb.WriteString(fmt.Sprintf(`Exec { gh ado2gh create-team --github-org "%s" --team-name "%s-Admins" --idp-group "%s-Admins" }`, GITHUB_ORG, ADO_TEAM_PROJECT, ADO_TEAM_PROJECT) + "\n")
	sb.WriteString("\n")
	sb.WriteString(fmt.Sprintf(`Exec { gh ado2gh lock-ado-repo --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" }`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO) + "\n")
	sb.WriteString(fmt.Sprintf(`$MigrationID = ExecAndGetMigrationID { gh ado2gh migrate-repo --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" --github-org "%s" --github-repo "%s-%s" --queue-only --target-repo-visibility private }`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO) + "\n")
	sb.WriteString(fmt.Sprintf(`$RepoMigrations["%s/%s-%s"] = $MigrationID`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO) + "\n")
	sb.WriteString("\n")
	fmt.Fprintf(&sb, "# =========== Waiting for all migrations to finish for Organization: %s ===========\n", ADO_ORG)
	sb.WriteString("\n")
	fmt.Fprintf(&sb, "# === Waiting for repo migration to finish for Team Project: %s and Repo: %s. Will then complete the below post migration steps. ===\n", ADO_TEAM_PROJECT, FOO_REPO)
	sb.WriteString("$CanExecuteBatch = $false\n")
	sb.WriteString(fmt.Sprintf(`if ($null -ne $RepoMigrations["%s/%s-%s"]) {`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO) + "\n")
	sb.WriteString(fmt.Sprintf(`    gh ado2gh wait-for-migration --migration-id $RepoMigrations["%s/%s-%s"]`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO) + "\n")
	sb.WriteString("    $CanExecuteBatch = ($lastexitcode -eq 0)\n")
	sb.WriteString("}\n")
	sb.WriteString("if ($CanExecuteBatch) {\n")
	sb.WriteString("    ExecBatch @(\n")
	sb.WriteString(fmt.Sprintf(`        { gh ado2gh disable-ado-repo --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" }`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO) + "\n")
	sb.WriteString(fmt.Sprintf(`        { gh ado2gh add-team-to-repo --github-org "%s" --github-repo "%s-%s" --team "%s-Maintainers" --role "maintain" }`, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO, ADO_TEAM_PROJECT) + "\n")
	sb.WriteString(fmt.Sprintf(`        { gh ado2gh add-team-to-repo --github-org "%s" --github-repo "%s-%s" --team "%s-Admins" --role "admin" }`, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO, ADO_TEAM_PROJECT) + "\n")
	sb.WriteString(fmt.Sprintf(`        { gh ado2gh download-logs --github-org "%s" --github-repo "%s-%s" }`, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO) + "\n")
	sb.WriteString("    )\n")
	sb.WriteString("    if ($Global:LastBatchFailures -eq 0) { $Succeeded++ }\n")
	sb.WriteString("} else {\n")
	sb.WriteString("    $Failed++\n")
	sb.WriteString("}\n")
	sb.WriteString("\n")
	sb.WriteString("Write-Host =============== Summary ===============\n")
	sb.WriteString("Write-Host Total number of successful migrations: $Succeeded\n")
	sb.WriteString("Write-Host Total number of failed migrations: $Failed\n")
	sb.WriteString("\nif ($Failed -ne 0) {\n    exit 1\n}\n")
	sb.WriteString("\n")
	sb.WriteString("\n")

	assert.Equal(t, sb.String(), output)
}

func TestParallelScript_Create_Teams_Option(t *testing.T) {
	inspector := &mockGenScriptInspector{
		repoCount:    1,
		orgs:         []string{ADO_ORG},
		teamProjects: map[string][]string{ADO_ORG: {ADO_TEAM_PROJECT}},
		repos:        map[string][]ado.Repository{ADO_ORG + "/" + ADO_TEAM_PROJECT: {{Name: FOO_REPO}}},
	}
	adoAPI := &mockGenScriptAdoAPI{}

	output := runGenScript(t, adoAPI, inspector, false,
		"--github-org", GITHUB_ORG,
		"--ado-org", ADO_ORG,
		"--output", "unit-test-output",
		"--create-teams",
	)

	trimmed := trimNonExecutableLines(output, 47, 6)

	lines := []string{
		fmt.Sprintf(`Exec { gh ado2gh create-team --github-org "%s" --team-name "%s-Maintainers" }`, GITHUB_ORG, ADO_TEAM_PROJECT),
		fmt.Sprintf(`Exec { gh ado2gh create-team --github-org "%s" --team-name "%s-Admins" }`, GITHUB_ORG, ADO_TEAM_PROJECT),
		fmt.Sprintf(`$MigrationID = ExecAndGetMigrationID { gh ado2gh migrate-repo --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" --github-org "%s" --github-repo "%s-%s" --queue-only --target-repo-visibility private }`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO),
		fmt.Sprintf(`$RepoMigrations["%s/%s-%s"] = $MigrationID`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO),
		"$CanExecuteBatch = $false",
		fmt.Sprintf(`if ($null -ne $RepoMigrations["%s/%s-%s"]) {`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO),
		fmt.Sprintf(`    gh ado2gh wait-for-migration --migration-id $RepoMigrations["%s/%s-%s"]`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO),
		"    $CanExecuteBatch = ($lastexitcode -eq 0)",
		"}",
		"if ($CanExecuteBatch) {",
		"    ExecBatch @(",
		fmt.Sprintf(`        { gh ado2gh add-team-to-repo --github-org "%s" --github-repo "%s-%s" --team "%s-Maintainers" --role "maintain" }`, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO, ADO_TEAM_PROJECT),
		fmt.Sprintf(`        { gh ado2gh add-team-to-repo --github-org "%s" --github-repo "%s-%s" --team "%s-Admins" --role "admin" }`, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO, ADO_TEAM_PROJECT),
		"    )",
		"    if ($Global:LastBatchFailures -eq 0) { $Succeeded++ }",
		"} else {",
		"    $Failed++",
		"}",
	}
	expected := strings.Join(lines, "\n")

	assert.Equal(t, expected, trimmed)
}

func TestParallelScript_Link_Idp_Groups_Option(t *testing.T) {
	inspector := &mockGenScriptInspector{
		repoCount:    1,
		orgs:         []string{ADO_ORG},
		teamProjects: map[string][]string{ADO_ORG: {ADO_TEAM_PROJECT}},
		repos:        map[string][]ado.Repository{ADO_ORG + "/" + ADO_TEAM_PROJECT: {{Name: FOO_REPO}}},
	}
	adoAPI := &mockGenScriptAdoAPI{}

	output := runGenScript(t, adoAPI, inspector, false,
		"--github-org", GITHUB_ORG,
		"--ado-org", ADO_ORG,
		"--output", "unit-test-output",
		"--link-idp-groups",
	)

	trimmed := trimNonExecutableLines(output, 47, 6)

	lines := []string{
		fmt.Sprintf(`Exec { gh ado2gh create-team --github-org "%s" --team-name "%s-Maintainers" --idp-group "%s-Maintainers" }`, GITHUB_ORG, ADO_TEAM_PROJECT, ADO_TEAM_PROJECT),
		fmt.Sprintf(`Exec { gh ado2gh create-team --github-org "%s" --team-name "%s-Admins" --idp-group "%s-Admins" }`, GITHUB_ORG, ADO_TEAM_PROJECT, ADO_TEAM_PROJECT),
		fmt.Sprintf(`$MigrationID = ExecAndGetMigrationID { gh ado2gh migrate-repo --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" --github-org "%s" --github-repo "%s-%s" --queue-only --target-repo-visibility private }`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO),
		fmt.Sprintf(`$RepoMigrations["%s/%s-%s"] = $MigrationID`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO),
		"$CanExecuteBatch = $false",
		fmt.Sprintf(`if ($null -ne $RepoMigrations["%s/%s-%s"]) {`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO),
		fmt.Sprintf(`    gh ado2gh wait-for-migration --migration-id $RepoMigrations["%s/%s-%s"]`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO),
		"    $CanExecuteBatch = ($lastexitcode -eq 0)",
		"}",
		"if ($CanExecuteBatch) {",
		"    ExecBatch @(",
		fmt.Sprintf(`        { gh ado2gh add-team-to-repo --github-org "%s" --github-repo "%s-%s" --team "%s-Maintainers" --role "maintain" }`, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO, ADO_TEAM_PROJECT),
		fmt.Sprintf(`        { gh ado2gh add-team-to-repo --github-org "%s" --github-repo "%s-%s" --team "%s-Admins" --role "admin" }`, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO, ADO_TEAM_PROJECT),
		"    )",
		"    if ($Global:LastBatchFailures -eq 0) { $Succeeded++ }",
		"} else {",
		"    $Failed++",
		"}",
	}
	expected := strings.Join(lines, "\n")

	assert.Equal(t, expected, trimmed)
}

func TestParallelScript_Lock_Ado_Repo_Option(t *testing.T) {
	inspector := &mockGenScriptInspector{
		repoCount:    1,
		orgs:         []string{ADO_ORG},
		teamProjects: map[string][]string{ADO_ORG: {ADO_TEAM_PROJECT}},
		repos:        map[string][]ado.Repository{ADO_ORG + "/" + ADO_TEAM_PROJECT: {{Name: FOO_REPO}}},
	}
	adoAPI := &mockGenScriptAdoAPI{}

	output := runGenScript(t, adoAPI, inspector, false,
		"--github-org", GITHUB_ORG,
		"--ado-org", ADO_ORG,
		"--output", "unit-test-output",
		"--lock-ado-repos",
	)

	trimmed := trimNonExecutableLines(output, 47, 6)

	lines := []string{
		fmt.Sprintf(`Exec { gh ado2gh lock-ado-repo --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" }`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO),
		fmt.Sprintf(`$MigrationID = ExecAndGetMigrationID { gh ado2gh migrate-repo --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" --github-org "%s" --github-repo "%s-%s" --queue-only --target-repo-visibility private }`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO),
		fmt.Sprintf(`$RepoMigrations["%s/%s-%s"] = $MigrationID`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO),
		"$CanExecuteBatch = $false",
		fmt.Sprintf(`if ($null -ne $RepoMigrations["%s/%s-%s"]) {`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO),
		fmt.Sprintf(`    gh ado2gh wait-for-migration --migration-id $RepoMigrations["%s/%s-%s"]`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO),
		"    $CanExecuteBatch = ($lastexitcode -eq 0)",
		"}",
		"if ($CanExecuteBatch) {",
		"    $Succeeded++",
		"} else {",
		"    $Failed++",
		"}",
	}
	expected := strings.Join(lines, "\n")

	assert.Equal(t, expected, trimmed)
}

func TestParallelScript_Disable_Ado_Repo_Option(t *testing.T) {
	inspector := &mockGenScriptInspector{
		repoCount:    1,
		orgs:         []string{ADO_ORG},
		teamProjects: map[string][]string{ADO_ORG: {ADO_TEAM_PROJECT}},
		repos:        map[string][]ado.Repository{ADO_ORG + "/" + ADO_TEAM_PROJECT: {{Name: FOO_REPO}}},
	}
	adoAPI := &mockGenScriptAdoAPI{}

	output := runGenScript(t, adoAPI, inspector, false,
		"--github-org", GITHUB_ORG,
		"--ado-org", ADO_ORG,
		"--output", "unit-test-output",
		"--disable-ado-repos",
	)

	trimmed := trimNonExecutableLines(output, 47, 6)

	lines := []string{
		fmt.Sprintf(`$MigrationID = ExecAndGetMigrationID { gh ado2gh migrate-repo --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" --github-org "%s" --github-repo "%s-%s" --queue-only --target-repo-visibility private }`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO),
		fmt.Sprintf(`$RepoMigrations["%s/%s-%s"] = $MigrationID`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO),
		"$CanExecuteBatch = $false",
		fmt.Sprintf(`if ($null -ne $RepoMigrations["%s/%s-%s"]) {`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO),
		fmt.Sprintf(`    gh ado2gh wait-for-migration --migration-id $RepoMigrations["%s/%s-%s"]`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO),
		"    $CanExecuteBatch = ($lastexitcode -eq 0)",
		"}",
		"if ($CanExecuteBatch) {",
		"    ExecBatch @(",
		fmt.Sprintf(`        { gh ado2gh disable-ado-repo --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" }`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO),
		"    )",
		"    if ($Global:LastBatchFailures -eq 0) { $Succeeded++ }",
		"} else {",
		"    $Failed++",
		"}",
	}
	expected := strings.Join(lines, "\n")

	assert.Equal(t, expected, trimmed)
}

func TestParallelScript_Rewire_Pipelines_Option(t *testing.T) {
	inspector := &mockGenScriptInspector{
		repoCount:    1,
		orgs:         []string{ADO_ORG},
		teamProjects: map[string][]string{ADO_ORG: {ADO_TEAM_PROJECT}},
		repos:        map[string][]ado.Repository{ADO_ORG + "/" + ADO_TEAM_PROJECT: {{Name: FOO_REPO}}},
		pipelines:    map[string][]string{ADO_ORG + "/" + ADO_TEAM_PROJECT + "/" + FOO_REPO: {FOO_PIPELINE}},
	}
	adoAPI := &mockGenScriptAdoAPI{
		getTeamProjectsResult: map[string][]string{ADO_ORG: {ADO_TEAM_PROJECT}},
		getGithubAppIdResult:  map[string]string{ADO_ORG: APP_ID},
	}

	output := runGenScript(t, adoAPI, inspector, false,
		"--github-org", GITHUB_ORG,
		"--ado-org", ADO_ORG,
		"--output", "unit-test-output",
		"--rewire-pipelines",
	)

	trimmed := trimNonExecutableLines(output, 47, 6)

	lines := []string{
		fmt.Sprintf(`Exec { gh ado2gh share-service-connection --ado-org "%s" --ado-team-project "%s" --service-connection-id "%s" }`, ADO_ORG, ADO_TEAM_PROJECT, APP_ID),
		fmt.Sprintf(`$MigrationID = ExecAndGetMigrationID { gh ado2gh migrate-repo --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" --github-org "%s" --github-repo "%s-%s" --queue-only --target-repo-visibility private }`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO),
		fmt.Sprintf(`$RepoMigrations["%s/%s-%s"] = $MigrationID`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO),
		"$CanExecuteBatch = $false",
		fmt.Sprintf(`if ($null -ne $RepoMigrations["%s/%s-%s"]) {`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO),
		fmt.Sprintf(`    gh ado2gh wait-for-migration --migration-id $RepoMigrations["%s/%s-%s"]`, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO),
		"    $CanExecuteBatch = ($lastexitcode -eq 0)",
		"}",
		"if ($CanExecuteBatch) {",
		"    ExecBatch @(",
		fmt.Sprintf(`        { gh ado2gh rewire-pipeline --ado-org "%s" --ado-team-project "%s" --ado-pipeline "%s" --github-org "%s" --github-repo "%s-%s" --service-connection-id "%s" }`, ADO_ORG, ADO_TEAM_PROJECT, FOO_PIPELINE, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO, APP_ID),
		"    )",
		"    if ($Global:LastBatchFailures -eq 0) { $Succeeded++ }",
		"} else {",
		"    $Failed++",
		"}",
	}
	expected := strings.Join(lines, "\n")

	assert.Equal(t, expected, trimmed)
}

func TestSequentialScript_CreateTeams_With_TargetApiUrl(t *testing.T) {
	inspector := &mockGenScriptInspector{
		repoCount:    1,
		orgs:         []string{ADO_ORG},
		teamProjects: map[string][]string{ADO_ORG: {ADO_TEAM_PROJECT}},
		repos:        map[string][]ado.Repository{ADO_ORG + "/" + ADO_TEAM_PROJECT: {{Name: FOO_REPO}}},
	}
	adoAPI := &mockGenScriptAdoAPI{}

	targetAPIURL := "https://example.com/api/v3"
	output := runGenScript(t, adoAPI, inspector, false,
		"--github-org", GITHUB_ORG,
		"--ado-org", ADO_ORG,
		"--sequential",
		"--output", "unit-test-output",
		"--create-teams",
		"--target-api-url", targetAPIURL,
	)

	trimmed := trimNonExecutableLines(output, 21, 0)

	lines := []string{
		fmt.Sprintf(`Exec { gh ado2gh create-team --target-api-url "%s" --github-org "%s" --team-name "%s-Maintainers" }`, targetAPIURL, GITHUB_ORG, ADO_TEAM_PROJECT),
		fmt.Sprintf(`Exec { gh ado2gh create-team --target-api-url "%s" --github-org "%s" --team-name "%s-Admins" }`, targetAPIURL, GITHUB_ORG, ADO_TEAM_PROJECT),
		fmt.Sprintf(`Exec { gh ado2gh migrate-repo --target-api-url "%s" --ado-org "%s" --ado-team-project "%s" --ado-repo "%s" --github-org "%s" --github-repo "%s-%s" --target-repo-visibility private }`, targetAPIURL, ADO_ORG, ADO_TEAM_PROJECT, FOO_REPO, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO),
		fmt.Sprintf(`Exec { gh ado2gh add-team-to-repo --target-api-url "%s" --github-org "%s" --github-repo "%s-%s" --team "%s-Maintainers" --role "maintain" }`, targetAPIURL, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO, ADO_TEAM_PROJECT),
		fmt.Sprintf(`Exec { gh ado2gh add-team-to-repo --target-api-url "%s" --github-org "%s" --github-repo "%s-%s" --team "%s-Admins" --role "admin" }`, targetAPIURL, GITHUB_ORG, ADO_TEAM_PROJECT, FOO_REPO, ADO_TEAM_PROJECT),
	}
	expected := strings.Join(lines, "\n")

	assert.Equal(t, expected, trimmed)
}
