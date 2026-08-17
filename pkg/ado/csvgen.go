package ado

import (
	"context"
	"fmt"
	"net/url"
	"strings"
	"time"
)

// ---------------------------------------------------------------------------
// Interfaces for CSV generators
// ---------------------------------------------------------------------------

// CSVInspector is the interface for inspector methods needed by CSV generators.
type CSVInspector interface {
	GetOrgs(ctx context.Context) ([]string, error)
	GetTeamProjects(ctx context.Context, org string) ([]string, error)
	GetRepos(ctx context.Context, org, teamProject string) ([]Repository, error)
	GetPipelines(ctx context.Context, org, teamProject, repo string) ([]string, error)
	GetTeamProjectCountForOrg(ctx context.Context, org string) (int, error)
	GetRepoCountForOrg(ctx context.Context, org string) (int, error)
	GetPipelineCountForOrg(ctx context.Context, org string) (int, error)
	GetPullRequestCountForOrg(ctx context.Context, org string) (int, error)
	GetPipelineCountForTeamProject(ctx context.Context, org, tp string) (int, error)
	GetPullRequestCountForTeamProject(ctx context.Context, org, tp string) (int, error)
	GetPullRequestCount(ctx context.Context, org, tp, repo string) (int, error)
}

// CSVAdoAPI is the interface for direct ADO API methods needed by CSV generators.
type CSVAdoAPI interface {
	GetOrgOwner(ctx context.Context, org string) (string, error)
	IsCallerOrgAdmin(ctx context.Context, org string) (bool, error)
	GetLastPushDate(ctx context.Context, org, tp, repo string) (time.Time, error)
	GetPushersSince(ctx context.Context, org, tp, repo string, fromDate time.Time) ([]string, error)
	GetCommitCountSince(ctx context.Context, org, tp, repo string, fromDate time.Time) (int, error)
	GetPipelineId(ctx context.Context, org, tp, pipeline string) (int, error)
}

// ---------------------------------------------------------------------------
// GenerateOrgsCsv
// ---------------------------------------------------------------------------

// GenerateOrgsCsv generates a CSV report of ADO organizations.
func GenerateOrgsCsv(ctx context.Context, ins CSVInspector, api CSVAdoAPI, minimal bool) (string, error) {
	var sb strings.Builder

	if minimal {
		sb.WriteString("name,url,owner,teamproject-count,repo-count,pipeline-count,is-pat-org-admin\n")
	} else {
		sb.WriteString("name,url,owner,teamproject-count,repo-count,pipeline-count,is-pat-org-admin,pr-count\n")
	}

	orgs, err := ins.GetOrgs(ctx)
	if err != nil {
		return "", err
	}

	for _, org := range orgs {
		owner, err := api.GetOrgOwner(ctx, org)
		if err != nil {
			return "", err
		}

		tpCount, err := ins.GetTeamProjectCountForOrg(ctx, org)
		if err != nil {
			return "", err
		}

		repoCount, err := ins.GetRepoCountForOrg(ctx, org)
		if err != nil {
			return "", err
		}

		pipelineCount, err := ins.GetPipelineCountForOrg(ctx, org)
		if err != nil {
			return "", err
		}

		isAdmin, err := api.IsCallerOrgAdmin(ctx, org)
		if err != nil {
			return "", err
		}

		adminStr := "False"
		if isAdmin {
			adminStr = "True"
		}

		orgURL := fmt.Sprintf("https://dev.azure.com/%s", url.PathEscape(org))

		if minimal {
			fmt.Fprintf(&sb, "%q,%q,%q,%d,%d,%d,%s\n",
				org, orgURL, owner, tpCount, repoCount, pipelineCount, adminStr)
		} else {
			prCount, err := ins.GetPullRequestCountForOrg(ctx, org)
			if err != nil {
				return "", err
			}
			fmt.Fprintf(&sb, "%q,%q,%q,%d,%d,%d,%s,%d\n",
				org, orgURL, owner, tpCount, repoCount, pipelineCount, adminStr, prCount)
		}
	}

	return sb.String(), nil
}

// ---------------------------------------------------------------------------
// GenerateTeamProjectsCsv
// ---------------------------------------------------------------------------

// GenerateTeamProjectsCsv generates a CSV report of ADO team projects.
func GenerateTeamProjectsCsv(ctx context.Context, ins CSVInspector, api CSVAdoAPI, minimal bool) (string, error) {
	_ = api // api not needed for team projects CSV but kept for interface consistency

	var sb strings.Builder

	if minimal {
		sb.WriteString("org,teamproject,url,repo-count,pipeline-count\n")
	} else {
		sb.WriteString("org,teamproject,url,repo-count,pipeline-count,pr-count\n")
	}

	orgs, err := ins.GetOrgs(ctx)
	if err != nil {
		return "", err
	}

	for _, org := range orgs {
		tps, err := ins.GetTeamProjects(ctx, org)
		if err != nil {
			return "", err
		}

		for _, tp := range tps {
			// Repo count for this team project: len(repos)
			repos, err := ins.GetRepos(ctx, org, tp)
			if err != nil {
				return "", err
			}
			repoCount := len(repos)

			pipelineCount, err := ins.GetPipelineCountForTeamProject(ctx, org, tp)
			if err != nil {
				return "", err
			}

			tpURL := fmt.Sprintf("https://dev.azure.com/%s/%s", url.PathEscape(org), url.PathEscape(tp))

			if minimal {
				fmt.Fprintf(&sb, "%q,%q,%q,%d,%d\n",
					org, tp, tpURL, repoCount, pipelineCount)
			} else {
				prCount, err := ins.GetPullRequestCountForTeamProject(ctx, org, tp)
				if err != nil {
					return "", err
				}
				fmt.Fprintf(&sb, "%q,%q,%q,%d,%d,%d\n",
					org, tp, tpURL, repoCount, pipelineCount, prCount)
			}
		}
	}

	return sb.String(), nil
}

// ---------------------------------------------------------------------------
// GenerateReposCsv
// ---------------------------------------------------------------------------

// GenerateReposCsv generates a CSV report of ADO repositories.
func GenerateReposCsv(ctx context.Context, ins CSVInspector, api CSVAdoAPI, minimal bool) (string, error) {
	var sb strings.Builder

	if minimal {
		sb.WriteString("org,teamproject,repo,url,last-push-date,pipeline-count,compressed-repo-size-in-bytes\n")
	} else {
		sb.WriteString("org,teamproject,repo,url,last-push-date,pipeline-count,compressed-repo-size-in-bytes,most-active-contributor,pr-count,commits-past-year\n")
	}

	orgs, err := ins.GetOrgs(ctx)
	if err != nil {
		return "", err
	}

	for _, org := range orgs {
		tps, err := ins.GetTeamProjects(ctx, org)
		if err != nil {
			return "", err
		}

		for _, tp := range tps {
			repos, err := ins.GetRepos(ctx, org, tp)
			if err != nil {
				return "", err
			}

			for _, repo := range repos {
				lastPush, err := api.GetLastPushDate(ctx, org, tp, repo.Name)
				if err != nil {
					return "", err
				}

				pipelines, err := ins.GetPipelines(ctx, org, tp, repo.Name)
				if err != nil {
					return "", err
				}
				pipelineCount := len(pipelines)

				repoURL := fmt.Sprintf("https://dev.azure.com/%s/%s/_git/%s",
					url.PathEscape(org), url.PathEscape(tp), url.PathEscape(repo.Name))

				// Format date as dd-MMM-yyyy hh:mm tt
				dateStr := lastPush.Format("02-Jan-2006 03:04 PM")

				sizeStr := formatWithThousandsSeparator(repo.Size)

				if minimal {
					fmt.Fprintf(&sb, "%q,%q,%q,%q,%q,%d,%q\n",
						org, tp, repo.Name, repoURL, dateStr, pipelineCount, sizeStr)
				} else {
					oneYearAgo := time.Now().AddDate(-1, 0, 0)

					pushers, err := api.GetPushersSince(ctx, org, tp, repo.Name, oneYearAgo)
					if err != nil {
						return "", err
					}
					contributor := getMostActiveContributor(pushers)

					prCount, err := ins.GetPullRequestCount(ctx, org, tp, repo.Name)
					if err != nil {
						return "", err
					}

					commitCount, err := api.GetCommitCountSince(ctx, org, tp, repo.Name, oneYearAgo)
					if err != nil {
						return "", err
					}

					fmt.Fprintf(&sb, "%q,%q,%q,%q,%q,%d,%q,%q,%d,%d\n",
						org, tp, repo.Name, repoURL, dateStr, pipelineCount, sizeStr,
						contributor, prCount, commitCount)
				}
			}
		}
	}

	return sb.String(), nil
}

// ---------------------------------------------------------------------------
// GeneratePipelinesCsv
// ---------------------------------------------------------------------------

// GeneratePipelinesCsv generates a CSV report of ADO pipelines.
func GeneratePipelinesCsv(ctx context.Context, ins CSVInspector, api CSVAdoAPI) (string, error) {
	var sb strings.Builder

	sb.WriteString("org,teamproject,repo,pipeline,url\n")

	orgs, err := ins.GetOrgs(ctx)
	if err != nil {
		return "", err
	}

	for _, org := range orgs {
		tps, err := ins.GetTeamProjects(ctx, org)
		if err != nil {
			return "", err
		}

		for _, tp := range tps {
			repos, err := ins.GetRepos(ctx, org, tp)
			if err != nil {
				return "", err
			}

			for _, repo := range repos {
				pipelines, err := ins.GetPipelines(ctx, org, tp, repo.Name)
				if err != nil {
					return "", err
				}

				for _, pipeline := range pipelines {
					pipelineID, err := api.GetPipelineId(ctx, org, tp, pipeline)
					if err != nil {
						return "", err
					}

					pipelineURL := fmt.Sprintf("https://dev.azure.com/%s/%s/_build?definitionId=%d",
						url.PathEscape(org), url.PathEscape(tp), pipelineID)

					fmt.Fprintf(&sb, "%q,%q,%q,%q,%q\n",
						org, tp, repo.Name, pipeline, pipelineURL)
				}
			}
		}
	}

	return sb.String(), nil
}

// ---------------------------------------------------------------------------
// Helper functions
// ---------------------------------------------------------------------------

// formatWithThousandsSeparator formats a number with comma thousands separators.
func formatWithThousandsSeparator(n uint64) string {
	s := fmt.Sprintf("%d", n)
	if len(s) <= 3 {
		return s
	}

	var result strings.Builder
	remainder := len(s) % 3
	if remainder > 0 {
		result.WriteString(s[:remainder])
	}

	for i := remainder; i < len(s); i += 3 {
		if result.Len() > 0 {
			result.WriteByte(',')
		}
		result.WriteString(s[i : i+3])
	}

	return result.String()
}

// getMostActiveContributor returns the most frequent pusher, filtering out
// entries containing "Service" (case-sensitive). Returns "N/A" if none remain.
func getMostActiveContributor(pushers []string) string {
	counts := make(map[string]int)
	for _, p := range pushers {
		if strings.Contains(p, "Service") {
			continue
		}
		counts[p]++
	}

	if len(counts) == 0 {
		return "N/A"
	}

	var best string
	var bestCount int
	for name, count := range counts {
		if count > bestCount {
			best = name
			bestCount = count
		}
	}
	return best
}
