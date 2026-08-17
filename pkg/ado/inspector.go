package ado

import (
	"context"
	"encoding/csv"
	"fmt"
	"io"
	"os"
	"regexp"

	"github.com/github/gh-gei/pkg/logger"
)

// inspectorAPI defines the subset of ADO client methods used by Inspector.
type inspectorAPI interface {
	GetUserId(ctx context.Context) (string, error)
	GetOrganizations(ctx context.Context, userId string) ([]string, error)
	GetTeamProjects(ctx context.Context, org string) ([]string, error)
	GetEnabledRepos(ctx context.Context, org, teamProject string) ([]Repository, error)
	PopulateRepoIdCache(ctx context.Context, org, teamProject string) error
	GetRepoId(ctx context.Context, org, teamProject, repo string) (string, error)
	GetPipelines(ctx context.Context, org, teamProject, repoId string) ([]string, error)
	GetPullRequestCount(ctx context.Context, org, teamProject, repo string) (int, error)
}

// Inspector is a caching layer over the ADO client that filters and caches
// org/team-project/repo/pipeline data. It corresponds to the C#
// AdoInspectorService class.
type Inspector struct {
	log *logger.Logger
	api inspectorAPI

	// OpenFileStream is overridable for testing.
	OpenFileStream func(string) (io.ReadCloser, error)

	// Filters — when set, restrict discovery to a single value.
	OrgFilter         string
	TeamProjectFilter string
	RepoFilter        string

	// Caches. orgs == nil means "not loaded yet"; a non-nil empty slice means
	// "loaded but empty" (e.g. CSV was loaded with no rows).
	orgs         []string
	orgsLoaded   bool // distinguishes nil "not loaded" from nil "loaded empty"
	teamProjects map[string][]string
	repos        map[string]map[string][]Repository
	pipelines    map[string]map[string]map[string][]string
	prCounts     map[string]map[string]map[string]int
}

// NewInspector creates an Inspector with initialized caches.
func NewInspector(log *logger.Logger, api inspectorAPI) *Inspector {
	return &Inspector{
		log:            log,
		api:            api,
		OpenFileStream: func(path string) (io.ReadCloser, error) { return os.Open(path) },
		teamProjects:   make(map[string][]string),
		repos:          make(map[string]map[string][]Repository),
		pipelines:      make(map[string]map[string]map[string][]string),
		prCounts:       make(map[string]map[string]map[string]int),
	}
}

// LoadReposCsv parses a CSV file (columns: org, teamproject, repo) and
// populates the caches. The header row is skipped.
func (ins *Inspector) LoadReposCsv(csvPath string) error {
	rc, err := ins.OpenFileStream(csvPath)
	if err != nil {
		return fmt.Errorf("open CSV %s: %w", csvPath, err)
	}
	defer rc.Close()

	reader := csv.NewReader(rc)

	// Skip header row.
	if _, err := reader.Read(); err != nil {
		return fmt.Errorf("read CSV header: %w", err)
	}

	// Mark orgs as loaded (even if CSV has no data rows).
	ins.orgs = []string{}
	ins.orgsLoaded = true

	for {
		fields, err := reader.Read()
		if err == io.EOF {
			break
		}
		if err != nil {
			return fmt.Errorf("read CSV row: %w", err)
		}
		if len(fields) < 3 {
			continue
		}

		org := fields[0]
		teamProject := fields[1]
		repo := fields[2]

		// Deduplicate org.
		if !containsString(ins.orgs, org) {
			ins.orgs = append(ins.orgs, org)
		}

		// Ensure team project maps exist.
		if _, ok := ins.teamProjects[org]; !ok {
			ins.teamProjects[org] = []string{}
		}
		if _, ok := ins.repos[org]; !ok {
			ins.repos[org] = make(map[string][]Repository)
		}

		// Deduplicate team project.
		if !containsString(ins.teamProjects[org], teamProject) {
			ins.teamProjects[org] = append(ins.teamProjects[org], teamProject)
		}

		// Ensure repo slice exists.
		if _, ok := ins.repos[org][teamProject]; !ok {
			ins.repos[org][teamProject] = []Repository{}
		}

		// Deduplicate repo by name.
		if !containsRepo(ins.repos[org][teamProject], repo) {
			ins.repos[org][teamProject] = append(ins.repos[org][teamProject], Repository{Name: repo})
		}
	}

	return nil
}

// GetOrgs returns the list of organizations, either from cache, filter, or API discovery.
func (ins *Inspector) GetOrgs(ctx context.Context) ([]string, error) {
	if ins.orgsLoaded {
		return ins.orgs, nil
	}

	if ins.OrgFilter != "" {
		ins.orgs = []string{ins.OrgFilter}
		ins.orgsLoaded = true
		return ins.orgs, nil
	}

	ins.log.Info("Retrieving list of all Orgs PAT has access to...")
	userId, err := ins.api.GetUserId(ctx)
	if err != nil {
		return nil, err
	}
	orgs, err := ins.api.GetOrganizations(ctx, userId)
	if err != nil {
		return nil, err
	}
	ins.orgs = orgs
	ins.orgsLoaded = true
	return ins.orgs, nil
}

// GetTeamProjects returns team projects for an org, cached or from API.
func (ins *Inspector) GetTeamProjects(ctx context.Context, org string) ([]string, error) {
	if tps, ok := ins.teamProjects[org]; ok {
		return tps, nil
	}

	var tps []string
	if ins.TeamProjectFilter != "" {
		tps = []string{ins.TeamProjectFilter}
	} else {
		var err error
		tps, err = ins.api.GetTeamProjects(ctx, org)
		if err != nil {
			return nil, err
		}
	}
	ins.teamProjects[org] = tps
	return tps, nil
}

// GetRepos returns repositories for an org/team-project, cached or from API.
func (ins *Inspector) GetRepos(ctx context.Context, org, teamProject string) ([]Repository, error) {
	if _, ok := ins.repos[org]; !ok {
		ins.repos[org] = make(map[string][]Repository)
	}
	if repos, ok := ins.repos[org][teamProject]; ok {
		return repos, nil
	}

	repos, err := ins.api.GetEnabledRepos(ctx, org, teamProject)
	if err != nil {
		return nil, err
	}
	ins.repos[org][teamProject] = repos
	return repos, nil
}

// GetPipelines returns pipelines for a repo, cached or from API.
func (ins *Inspector) GetPipelines(ctx context.Context, org, teamProject, repo string) ([]string, error) {
	if _, ok := ins.pipelines[org]; !ok {
		ins.pipelines[org] = make(map[string]map[string][]string)
	}
	if _, ok := ins.pipelines[org][teamProject]; !ok {
		ins.pipelines[org][teamProject] = make(map[string][]string)
	}
	if p, ok := ins.pipelines[org][teamProject][repo]; ok {
		return p, nil
	}

	if err := ins.api.PopulateRepoIdCache(ctx, org, teamProject); err != nil {
		return nil, err
	}
	repoId, err := ins.api.GetRepoId(ctx, org, teamProject, repo)
	if err != nil {
		return nil, err
	}
	pipelines, err := ins.api.GetPipelines(ctx, org, teamProject, repoId)
	if err != nil {
		return nil, err
	}
	ins.pipelines[org][teamProject][repo] = pipelines
	return pipelines, nil
}

// GetPullRequestCount returns the PR count for a single repo, cached or from API.
func (ins *Inspector) GetPullRequestCount(ctx context.Context, org, teamProject, repo string) (int, error) {
	if _, ok := ins.prCounts[org]; !ok {
		ins.prCounts[org] = make(map[string]map[string]int)
	}
	if _, ok := ins.prCounts[org][teamProject]; !ok {
		ins.prCounts[org][teamProject] = make(map[string]int)
	}
	if count, ok := ins.prCounts[org][teamProject][repo]; ok {
		return count, nil
	}

	count, err := ins.api.GetPullRequestCount(ctx, org, teamProject, repo)
	if err != nil {
		return 0, err
	}
	ins.prCounts[org][teamProject][repo] = count
	return count, nil
}

// SetOrgFilter sets the org filter for narrowing discovery to a single org.
func (ins *Inspector) SetOrgFilter(org string) {
	ins.OrgFilter = org
}

// GetOrgFilter returns the current org filter.
func (ins *Inspector) GetOrgFilter() string {
	return ins.OrgFilter
}

// ---------- Count aggregations ----------

// GetRepoCount returns the total number of repos across all orgs and team projects.
func (ins *Inspector) GetRepoCount(ctx context.Context) (int, error) {
	orgs, err := ins.GetOrgs(ctx)
	if err != nil {
		return 0, err
	}
	total := 0
	for _, org := range orgs {
		count, err := ins.GetRepoCountForOrg(ctx, org)
		if err != nil {
			return 0, err
		}
		total += count
	}
	return total, nil
}

// GetRepoCountForOrg returns the total number of repos in an org.
func (ins *Inspector) GetRepoCountForOrg(ctx context.Context, org string) (int, error) {
	tps, err := ins.GetTeamProjects(ctx, org)
	if err != nil {
		return 0, err
	}
	total := 0
	for _, tp := range tps {
		repos, err := ins.GetRepos(ctx, org, tp)
		if err != nil {
			return 0, err
		}
		total += len(repos)
	}
	return total, nil
}

// GetTeamProjectCount returns the total number of team projects across all orgs.
func (ins *Inspector) GetTeamProjectCount(ctx context.Context) (int, error) {
	orgs, err := ins.GetOrgs(ctx)
	if err != nil {
		return 0, err
	}
	total := 0
	for _, org := range orgs {
		count, err := ins.GetTeamProjectCountForOrg(ctx, org)
		if err != nil {
			return 0, err
		}
		total += count
	}
	return total, nil
}

// GetTeamProjectCountForOrg returns the number of team projects in an org.
func (ins *Inspector) GetTeamProjectCountForOrg(ctx context.Context, org string) (int, error) {
	tps, err := ins.GetTeamProjects(ctx, org)
	if err != nil {
		return 0, err
	}
	return len(tps), nil
}

// GetPipelineCount returns the total number of pipelines across all orgs.
func (ins *Inspector) GetPipelineCount(ctx context.Context) (int, error) {
	orgs, err := ins.GetOrgs(ctx)
	if err != nil {
		return 0, err
	}
	total := 0
	for _, org := range orgs {
		count, err := ins.GetPipelineCountForOrg(ctx, org)
		if err != nil {
			return 0, err
		}
		total += count
	}
	return total, nil
}

// GetPipelineCountForOrg returns the total number of pipelines in an org.
func (ins *Inspector) GetPipelineCountForOrg(ctx context.Context, org string) (int, error) {
	tps, err := ins.GetTeamProjects(ctx, org)
	if err != nil {
		return 0, err
	}
	total := 0
	for _, tp := range tps {
		count, err := ins.GetPipelineCountForTeamProject(ctx, org, tp)
		if err != nil {
			return 0, err
		}
		total += count
	}
	return total, nil
}

// GetPipelineCountForTeamProject returns the total number of pipelines in a team project.
func (ins *Inspector) GetPipelineCountForTeamProject(ctx context.Context, org, teamProject string) (int, error) {
	repos, err := ins.GetRepos(ctx, org, teamProject)
	if err != nil {
		return 0, err
	}
	total := 0
	for _, r := range repos {
		pipelines, err := ins.GetPipelines(ctx, org, teamProject, r.Name)
		if err != nil {
			return 0, err
		}
		total += len(pipelines)
	}
	return total, nil
}

// GetPullRequestCountForTeamProject returns the total PR count across all repos in a team project.
func (ins *Inspector) GetPullRequestCountForTeamProject(ctx context.Context, org, teamProject string) (int, error) {
	repos, err := ins.GetRepos(ctx, org, teamProject)
	if err != nil {
		return 0, err
	}
	total := 0
	for _, r := range repos {
		count, err := ins.GetPullRequestCount(ctx, org, teamProject, r.Name)
		if err != nil {
			return 0, err
		}
		total += count
	}
	return total, nil
}

// GetPullRequestCountForOrg returns the total PR count across all repos in an org.
func (ins *Inspector) GetPullRequestCountForOrg(ctx context.Context, org string) (int, error) {
	tps, err := ins.GetTeamProjects(ctx, org)
	if err != nil {
		return 0, err
	}
	total := 0
	for _, tp := range tps {
		count, err := ins.GetPullRequestCountForTeamProject(ctx, org, tp)
		if err != nil {
			return 0, err
		}
		total += count
	}
	return total, nil
}

// OutputRepoListToLog logs the cached repo hierarchy.
func (ins *Inspector) OutputRepoListToLog() {
	for org, tpMap := range ins.repos {
		ins.log.Info("ADO Org: %s", org)
		for tp, repos := range tpMap {
			ins.log.Info("  Team Project: %s", tp)
			for _, repo := range repos {
				ins.log.Info("    Repo: %s", repo.Name)
			}
		}
	}
}

// ---------- Utility ----------

var invalidCharsRe = regexp.MustCompile(`[^\w.\-]+`)

// ReplaceInvalidCharactersWithDash replaces sequences of characters that are
// not word characters, dots, or dashes with a single dash.
// Equivalent to C# Regex.Replace(s, @"[^\w.-]+", "-")
func ReplaceInvalidCharactersWithDash(s string) string {
	return invalidCharsRe.ReplaceAllString(s, "-")
}

// ---------- internal helpers ----------

func containsString(ss []string, s string) bool {
	for _, v := range ss {
		if v == s {
			return true
		}
	}
	return false
}

func containsRepo(repos []Repository, name string) bool {
	for _, r := range repos {
		if r.Name == name {
			return true
		}
	}
	return false
}
