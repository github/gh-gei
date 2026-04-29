package main

import (
	"context"
	"fmt"
	"net/url"
	"os"
	"strings"
	"time"

	"github.com/github/gh-gei/pkg/bbs"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/spf13/cobra"
)

// ---------------------------------------------------------------------------
// Consumer-defined interfaces & local types
// ---------------------------------------------------------------------------

// inventoryReportBbsAPI defines all BBS API methods needed by inventory-report.
type inventoryReportBbsAPI interface {
	GetProjects(ctx context.Context) ([]invProject, error)
	GetProject(ctx context.Context, projectKey string) (invProject, error)
	GetRepos(ctx context.Context, projectKey string) ([]invRepository, error)
	GetRepositoryPullRequests(ctx context.Context, projectKey, repo string) ([]invPullRequest, error)
	GetRepositoryLatestCommitDate(ctx context.Context, projectKey, repo string) (*time.Time, error)
	GetRepositoryAndAttachmentsSize(ctx context.Context, projectKey, repo string) (repoSize, attachmentsSize uint64, err error)
	GetIsRepositoryArchived(ctx context.Context, projectKey, repo string) (bool, error)
}

type invProject struct {
	ID   int
	Key  string
	Name string
}

type invRepository struct {
	ID   int
	Slug string
	Name string
}

type invPullRequest struct {
	ID   int
	Name string
}

// ---------------------------------------------------------------------------
// Args struct
// ---------------------------------------------------------------------------

type bbsInventoryReportArgs struct {
	bbsServerURL string
	bbsProject   string
	bbsUsername  string
	bbsPassword  string
	noSslVerify  bool
	minimal      bool
}

// ---------------------------------------------------------------------------
// Command constructor (testable)
// ---------------------------------------------------------------------------

func newInventoryReportCmd(
	api inventoryReportBbsAPI,
	log *logger.Logger,
	writeFile func(string, string) error,
) *cobra.Command {
	var a bbsInventoryReportArgs

	cmd := &cobra.Command{
		Use:   "inventory-report",
		Short: "Generates several CSV files containing lists of BBS projects and repos",
		Long: "Generates several CSV files containing lists of BBS projects and repos. " +
			"Useful for planning large migrations.",
		RunE: func(cmd *cobra.Command, _ []string) error {
			return runInventoryReport(cmd.Context(), api, log, a, writeFile)
		},
	}

	cmd.Flags().StringVar(&a.bbsServerURL, "bbs-server-url", "", "The full URL of the Bitbucket Server/Data Center instance (REQUIRED)")
	cmd.Flags().StringVar(&a.bbsProject, "bbs-project", "", "Only generate report for this BBS project")
	cmd.Flags().StringVar(&a.bbsUsername, "bbs-username", "", "BBS username for API authentication")
	cmd.Flags().StringVar(&a.bbsPassword, "bbs-password", "", "BBS password for API authentication")
	cmd.Flags().BoolVar(&a.noSslVerify, "no-ssl-verify", false, "Disable SSL verification for BBS API calls")
	cmd.Flags().BoolVar(&a.minimal, "minimal", false, "Omit PR counts and archived status for faster generation")

	return cmd
}

// ---------------------------------------------------------------------------
// Production command constructor
// ---------------------------------------------------------------------------

func newInventoryReportCmdLive() *cobra.Command {
	var a bbsInventoryReportArgs

	cmd := &cobra.Command{
		Use:   "inventory-report",
		Short: "Generates several CSV files containing lists of BBS projects and repos",
		Long: "Generates several CSV files containing lists of BBS projects and repos. " +
			"Useful for planning large migrations.",
		RunE: func(cmd *cobra.Command, _ []string) error {
			log := getLogger(cmd)
			envProv := getEnvProvider()

			bbsUser := a.bbsUsername
			if bbsUser == "" {
				bbsUser = envProv.BBSUsername()
			}
			bbsPass := a.bbsPassword
			if bbsPass == "" {
				bbsPass = envProv.BBSPassword()
			}

			bbsClient := bbs.NewClient(a.bbsServerURL, bbsUser, bbsPass, log)
			api := &invReportBbsClientAdapter{client: bbsClient}

			writeFile := func(path, content string) error {
				return os.WriteFile(path, []byte(content), 0o600)
			}

			return runInventoryReport(cmd.Context(), api, log, a, writeFile)
		},
	}

	cmd.Flags().StringVar(&a.bbsServerURL, "bbs-server-url", "", "The full URL of the Bitbucket Server/Data Center instance (REQUIRED)")
	cmd.Flags().StringVar(&a.bbsProject, "bbs-project", "", "Only generate report for this BBS project")
	cmd.Flags().StringVar(&a.bbsUsername, "bbs-username", "", "BBS username for API authentication")
	cmd.Flags().StringVar(&a.bbsPassword, "bbs-password", "", "BBS password for API authentication")
	cmd.Flags().BoolVar(&a.noSslVerify, "no-ssl-verify", false, "Disable SSL verification for BBS API calls")
	cmd.Flags().BoolVar(&a.minimal, "minimal", false, "Omit PR counts and archived status for faster generation")

	return cmd
}

// ---------------------------------------------------------------------------
// Adapter: wraps *bbs.Client → inventoryReportBbsAPI
// ---------------------------------------------------------------------------

type invReportBbsClientAdapter struct {
	client *bbs.Client
}

func (a *invReportBbsClientAdapter) GetProjects(ctx context.Context) ([]invProject, error) {
	projects, err := a.client.GetProjects(ctx)
	if err != nil {
		return nil, err
	}
	result := make([]invProject, len(projects))
	for i, p := range projects {
		result[i] = invProject{ID: p.ID, Key: p.Key, Name: p.Name}
	}
	return result, nil
}

func (a *invReportBbsClientAdapter) GetProject(ctx context.Context, projectKey string) (invProject, error) {
	p, err := a.client.GetProject(ctx, projectKey)
	if err != nil {
		return invProject{}, err
	}
	return invProject{ID: p.ID, Key: p.Key, Name: p.Name}, nil
}

func (a *invReportBbsClientAdapter) GetRepos(ctx context.Context, projectKey string) ([]invRepository, error) {
	repos, err := a.client.GetRepos(ctx, projectKey)
	if err != nil {
		return nil, err
	}
	result := make([]invRepository, len(repos))
	for i, r := range repos {
		result[i] = invRepository{ID: r.ID, Slug: r.Slug, Name: r.Name}
	}
	return result, nil
}

func (a *invReportBbsClientAdapter) GetRepositoryPullRequests(ctx context.Context, projectKey, repo string) ([]invPullRequest, error) {
	prs, err := a.client.GetRepositoryPullRequests(ctx, projectKey, repo)
	if err != nil {
		return nil, err
	}
	result := make([]invPullRequest, len(prs))
	for i, pr := range prs {
		result[i] = invPullRequest{ID: pr.ID, Name: pr.Name}
	}
	return result, nil
}

func (a *invReportBbsClientAdapter) GetRepositoryLatestCommitDate(ctx context.Context, projectKey, repo string) (*time.Time, error) {
	return a.client.GetRepositoryLatestCommitDate(ctx, projectKey, repo)
}

func (a *invReportBbsClientAdapter) GetRepositoryAndAttachmentsSize(ctx context.Context, projectKey, repo string) (uint64, uint64, error) {
	return a.client.GetRepositoryAndAttachmentsSize(ctx, projectKey, repo)
}

func (a *invReportBbsClientAdapter) GetIsRepositoryArchived(ctx context.Context, projectKey, repo string) (bool, error) {
	return a.client.GetIsRepositoryArchived(ctx, projectKey, repo)
}

// ---------------------------------------------------------------------------
// Runner
// ---------------------------------------------------------------------------

func runInventoryReport(
	ctx context.Context,
	api inventoryReportBbsAPI,
	log *logger.Logger,
	a bbsInventoryReportArgs,
	writeFile func(string, string) error,
) error {
	log.Info("Creating inventory report...")

	// Determine project keys
	if strings.TrimSpace(a.bbsProject) == "" {
		log.Info("Finding Projects...")
		projects, err := api.GetProjects(ctx)
		if err != nil {
			return err
		}
		log.Info("Found %d Projects", len(projects))
	}

	// Count repos
	log.Info("Finding Repos...")
	repoCount, err := countRepos(ctx, api, a.bbsProject)
	if err != nil {
		return err
	}
	log.Info("Found %d Repos", repoCount)

	// Generate projects CSV
	log.Info("Generating data for projects.csv...")
	projectsCsv, err := generateProjectsCSV(ctx, api, a.bbsServerURL, a.bbsProject, a.minimal)
	if err != nil {
		return err
	}
	if err := writeFile("projects.csv", projectsCsv); err != nil {
		return err
	}
	log.Info("projects.csv generated")

	// Generate repos CSV
	log.Info("Generating repos.csv...")
	reposCsv, err := generateReposCSV(ctx, api, a.bbsServerURL, a.bbsProject, a.minimal)
	if err != nil {
		return err
	}
	if err := writeFile("repos.csv", reposCsv); err != nil {
		return err
	}
	log.Info("repos.csv generated")

	return nil
}

// ---------------------------------------------------------------------------
// Repo counting helper (inline inspector logic)
// ---------------------------------------------------------------------------

func countRepos(ctx context.Context, api inventoryReportBbsAPI, bbsProject string) (int, error) {
	if strings.TrimSpace(bbsProject) != "" {
		repos, err := api.GetRepos(ctx, bbsProject)
		if err != nil {
			return 0, err
		}
		return len(repos), nil
	}

	projects, err := api.GetProjects(ctx)
	if err != nil {
		return 0, err
	}
	total := 0
	for _, p := range projects {
		repos, err := api.GetRepos(ctx, p.Key)
		if err != nil {
			return 0, err
		}
		total += len(repos)
	}
	return total, nil
}

// ---------------------------------------------------------------------------
// Projects CSV generator
// ---------------------------------------------------------------------------

func generateProjectsCSV(
	ctx context.Context,
	api inventoryReportBbsAPI,
	bbsServerURL string,
	bbsProject string,
	minimal bool,
) (string, error) {
	var sb strings.Builder

	// Header
	sb.WriteString("project-key,project-name,url,repo-count")
	if !minimal {
		sb.WriteString(",pr-count")
	}
	sb.WriteByte('\n')

	// Get projects
	projects, err := getProjectsList(ctx, api, bbsProject)
	if err != nil {
		return "", err
	}

	serverURL := strings.TrimRight(bbsServerURL, "/")

	for _, p := range projects {
		projectURL := fmt.Sprintf("%s/projects/%s", serverURL, url.PathEscape(p.Key))

		repos, err := api.GetRepos(ctx, p.Key)
		if err != nil {
			return "", err
		}
		repoCount := len(repos)

		prCount := 0
		if !minimal {
			prCount, err = countPRsForRepos(ctx, api, p.Key, repos)
			if err != nil {
				return "", err
			}
		}

		projectName := escapeCommas(p.Name)

		fmt.Fprintf(&sb, `"%s","%s","%s",%d`, p.Key, projectName, projectURL, repoCount)
		if !minimal {
			fmt.Fprintf(&sb, ",%d", prCount)
		}
		sb.WriteByte('\n')
	}

	return sb.String(), nil
}

// ---------------------------------------------------------------------------
// Repos CSV generator
// ---------------------------------------------------------------------------

type repoRow struct {
	line     string
	archived string // "True"/"False" or empty if failed/minimal
	prCount  int
}

// buildRepoRow fetches details for a single repo and builds a CSV row.
func buildRepoRow(
	ctx context.Context,
	api inventoryReportBbsAPI,
	serverURL string,
	p invProject,
	repo invRepository,
	minimal bool,
) (repoRow, error) {
	repoURL := fmt.Sprintf("%s/projects/%s/repos/%s",
		serverURL, url.PathEscape(p.Key), url.PathEscape(repo.Slug))

	lastCommitDate, err := api.GetRepositoryLatestCommitDate(ctx, p.Key, repo.Slug)
	if err != nil {
		return repoRow{}, err
	}

	repoSize, attachmentsSize, err := api.GetRepositoryAndAttachmentsSize(ctx, p.Key, repo.Slug)
	if err != nil {
		return repoRow{}, err
	}

	prCount := 0
	if !minimal {
		prs, prErr := api.GetRepositoryPullRequests(ctx, p.Key, repo.Slug)
		if prErr != nil {
			return repoRow{}, prErr
		}
		prCount = len(prs)
	}

	projectName := escapeCommas(p.Name)
	repoName := escapeCommas(repo.Name)

	var datePart string
	if lastCommitDate == nil {
		datePart = ""
	} else {
		datePart = fmt.Sprintf(`"%s"`, lastCommitDate.Format("2006-01-02 03:04 PM"))
	}

	line := fmt.Sprintf(`"%s","%s","%s","%s",%s,"%d","%d"`,
		p.Key, projectName, repoName, repoURL,
		datePart, repoSize, attachmentsSize)

	return repoRow{line: line, prCount: prCount}, nil
}

func generateReposCSV(
	ctx context.Context,
	api inventoryReportBbsAPI,
	bbsServerURL string,
	bbsProject string,
	minimal bool,
) (string, error) {
	var sb strings.Builder
	serverURL := strings.TrimRight(bbsServerURL, "/")

	includeArchived := !minimal
	archivedFailed := false

	sb.WriteString("project-key,project-name,repo,url,last-commit-date,repo-size-in-bytes,attachments-size-in-bytes")
	if !minimal {
		sb.WriteString(",is-archived,pr-count")
	}
	sb.WriteByte('\n')

	projects, err := getProjectsList(ctx, api, bbsProject)
	if err != nil {
		return "", err
	}

	var rows []repoRow

	for _, p := range projects {
		repos, err := api.GetRepos(ctx, p.Key)
		if err != nil {
			return "", err
		}

		for _, repo := range repos {
			row, err := buildRepoRow(ctx, api, serverURL, p, repo, minimal)
			if err != nil {
				return "", err
			}

			if includeArchived && !archivedFailed {
				archived, archErr := api.GetIsRepositoryArchived(ctx, p.Key, repo.Slug)
				if archErr != nil {
					archivedFailed = true
					includeArchived = false
				} else {
					row.archived = boolToTitleCase(archived)
				}
			}

			rows = append(rows, row)
		}
	}

	// If archived failed, rebuild header without ,is-archived
	if archivedFailed {
		sb.Reset()
		sb.WriteString("project-key,project-name,repo,url,last-commit-date,repo-size-in-bytes,attachments-size-in-bytes")
		if !minimal {
			sb.WriteString(",pr-count")
		}
		sb.WriteByte('\n')
	}

	// Write data rows
	for _, row := range rows {
		sb.WriteString(row.line)
		if !minimal {
			if includeArchived {
				fmt.Fprintf(&sb, `,"%s",%d`, row.archived, row.prCount)
			} else {
				fmt.Fprintf(&sb, ",%d", row.prCount)
			}
		}
		sb.WriteByte('\n')
	}

	return sb.String(), nil
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

// getProjectsList returns the list of projects to process.
func getProjectsList(ctx context.Context, api inventoryReportBbsAPI, bbsProject string) ([]invProject, error) {
	if strings.TrimSpace(bbsProject) != "" {
		p, err := api.GetProject(ctx, bbsProject)
		if err != nil {
			return nil, err
		}
		return []invProject{p}, nil
	}
	return api.GetProjects(ctx)
}

// countPRsForRepos counts all PRs across the given repos in a project.
func countPRsForRepos(ctx context.Context, api inventoryReportBbsAPI, projectKey string, repos []invRepository) (int, error) {
	total := 0
	for _, repo := range repos {
		prs, err := api.GetRepositoryPullRequests(ctx, projectKey, repo.Slug)
		if err != nil {
			return 0, err
		}
		total += len(prs)
	}
	return total, nil
}

// escapeCommas replaces commas with %2C in a string.
func escapeCommas(s string) string {
	return strings.ReplaceAll(s, ",", "%2C")
}

// boolToTitleCase returns "True" or "False" matching C# bool.ToString().
func boolToTitleCase(b bool) string {
	if b {
		return "True"
	}
	return "False"
}
