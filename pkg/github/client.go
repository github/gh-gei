// Package github provides a GitHub API client for the gh-gei migration tool.
// REST operations use go-github; GraphQL uses a thin custom client for migration-specific mutations.
package github

import (
	"context"
	"crypto/tls"
	"encoding/json"
	"fmt"
	"net/http"
	"net/url"
	"strings"

	gogithub "github.com/google/go-github/v68/github"

	"github.com/github/gh-gei/pkg/logger"
)

const defaultAPIURL = "https://api.github.com"

// Client is a GitHub API client that uses go-github for REST and a custom
// graphqlClient for migration-specific GraphQL operations.
type Client struct {
	rest    *gogithub.Client // go-github for REST
	graphql *graphqlClient   // custom for migration GraphQL
	logger  *logger.Logger
	apiURL  string
}

// Option configures a Client.
type Option func(*clientConfig)

type clientConfig struct {
	apiURL      string
	logger      *logger.Logger
	noSSLVerify bool
	version     string
}

// WithAPIURL sets the base API URL (for GHES). Defaults to https://api.github.com.
func WithAPIURL(u string) Option {
	return func(c *clientConfig) {
		c.apiURL = u
	}
}

// WithLogger sets the logger.
func WithLogger(l *logger.Logger) Option {
	return func(c *clientConfig) {
		c.logger = l
	}
}

// WithNoSSLVerify disables TLS verification (for GHES with self-signed certs).
func WithNoSSLVerify() Option {
	return func(c *clientConfig) {
		c.noSSLVerify = true
	}
}

// WithVersion sets the CLI version used in User-Agent headers.
func WithVersion(v string) Option {
	return func(c *clientConfig) {
		c.version = v
	}
}

// NewClient creates a new GitHub API client using a PAT for auth.
func NewClient(pat string, opts ...Option) *Client {
	cfg := &clientConfig{
		apiURL:  defaultAPIURL,
		logger:  logger.New(false),
		version: "0.0.0",
	}
	for _, o := range opts {
		o(cfg)
	}
	cfg.apiURL = strings.TrimRight(cfg.apiURL, "/")

	// Build HTTP transport
	transport := &http.Transport{}
	if cfg.noSSLVerify {
		transport.TLSClientConfig = &tls.Config{InsecureSkipVerify: true} // #nosec G402
	}
	httpClient := &http.Client{Transport: transport}

	// Configure go-github REST client
	restClient := gogithub.NewClient(httpClient).WithAuthToken(pat)
	if cfg.apiURL != defaultAPIURL {
		baseURL, err := url.Parse(cfg.apiURL + "/")
		if err != nil {
			cfg.logger.Warning("Failed to parse API URL %q, falling back to default: %v", cfg.apiURL, err)
		} else {
			restClient.BaseURL = baseURL
		}
	}

	// Configure custom GraphQL client
	gql := newGraphQLClient(cfg.apiURL, pat, cfg.version, cfg.logger)
	if cfg.noSSLVerify {
		gql.httpClient = httpClient
	}

	return &Client{
		rest:    restClient,
		graphql: gql,
		logger:  cfg.logger,
		apiURL:  cfg.apiURL,
	}
}

// GetRepos fetches all repositories for a given organization using go-github.
// go-github handles pagination natively via ListByOrg with ListOptions.
func (c *Client) GetRepos(ctx context.Context, org string) ([]Repo, error) {
	c.logger.Info("Fetching repositories for organization: %s", org)

	var allRepos []Repo
	opts := &gogithub.RepositoryListByOrgOptions{
		ListOptions: gogithub.ListOptions{PerPage: 100},
	}

	for {
		ghRepos, resp, err := c.rest.Repositories.ListByOrg(ctx, org, opts)
		if err != nil {
			return nil, fmt.Errorf("failed to fetch repos: %w", err)
		}

		for _, r := range ghRepos {
			allRepos = append(allRepos, Repo{
				Name:       r.GetName(),
				Visibility: r.GetVisibility(),
			})
		}

		c.logger.Debug("Fetched %d repos from page %d", len(ghRepos), opts.Page)

		if resp.NextPage == 0 {
			break
		}
		opts.Page = resp.NextPage
	}

	c.logger.Info("Found %d repositories in organization %s", len(allRepos), org)
	return allRepos, nil
}

// GetVersion fetches the GitHub Enterprise Server version.
// Only applicable for GHES — returns an error for GitHub.com.
// go-github's APIMeta doesn't include installed_version (GHES-specific),
// so we make a raw request via go-github and parse the field ourselves.
func (c *Client) GetVersion(ctx context.Context) (*VersionInfo, error) {
	if c.apiURL == defaultAPIURL {
		return nil, fmt.Errorf("version endpoint not available on GitHub.com")
	}

	req, err := c.rest.NewRequest("GET", "meta", nil)
	if err != nil {
		return nil, fmt.Errorf("failed to create version request: %w", err)
	}

	var meta struct {
		InstalledVersion string `json:"installed_version"`
	}
	_, err = c.rest.Do(ctx, req, &meta)
	if err != nil {
		return nil, fmt.Errorf("failed to fetch version: %w", err)
	}

	return &VersionInfo{
		Version:          meta.InstalledVersion,
		InstalledVersion: meta.InstalledVersion,
	}, nil
}

// GraphQL sends a GraphQL query and returns the raw "data" field.
func (c *Client) GraphQL(ctx context.Context, query string, variables json.RawMessage) (json.RawMessage, error) {
	return c.graphql.Post(ctx, query, variables)
}

// GraphQLWithPagination sends a paginated GraphQL query, collecting all pages.
func (c *Client) GraphQLWithPagination(
	ctx context.Context,
	query string,
	variables json.RawMessage,
	dataPath string,
	pageInfoPath string,
) (json.RawMessage, error) {
	return c.graphql.PostWithPagination(ctx, query, variables, dataPath, pageInfoPath)
}

// ---------------------------------------------------------------------------
// Organization / User queries
// ---------------------------------------------------------------------------

// GetOrganizationId returns the node ID (global ID) for the given organization.
func (c *Client) GetOrganizationId(ctx context.Context, org string) (string, error) {
	query := `query($login: String!) {organization(login: $login) { login, id, name } }`
	vars, _ := json.Marshal(map[string]string{"login": org})

	data, err := c.graphql.Post(ctx, query, vars)
	if err != nil {
		return "", fmt.Errorf("failed to get organization ID for %q: %w", org, err)
	}

	orgData, err := navigateJSON(data, "organization.id")
	if err != nil {
		return "", fmt.Errorf("failed to parse organization ID for %q: %w", org, err)
	}

	var id string
	if err := json.Unmarshal(orgData, &id); err != nil {
		return "", fmt.Errorf("failed to unmarshal organization ID for %q: %w", org, err)
	}
	return id, nil
}

// GetOrganizationDatabaseId returns the database ID (integer) for the given organization as a string.
func (c *Client) GetOrganizationDatabaseId(ctx context.Context, org string) (string, error) {
	query := `query($login: String!) {organization(login: $login) { login, databaseId, name } }`
	vars, _ := json.Marshal(map[string]string{"login": org})

	data, err := c.graphql.Post(ctx, query, vars)
	if err != nil {
		return "", fmt.Errorf("failed to get organization database ID for %q: %w", org, err)
	}

	dbIDRaw, err := navigateJSON(data, "organization.databaseId")
	if err != nil {
		return "", fmt.Errorf("failed to parse organization database ID for %q: %w", org, err)
	}

	// databaseId comes back as a JSON number — unmarshal to json.Number then convert to string.
	var num json.Number
	if err := json.Unmarshal(dbIDRaw, &num); err != nil {
		return "", fmt.Errorf("failed to unmarshal organization database ID for %q: %w", org, err)
	}
	return num.String(), nil
}

// GetEnterpriseId returns the node ID for the given enterprise.
func (c *Client) GetEnterpriseId(ctx context.Context, enterpriseName string) (string, error) {
	query := `query($slug: String!) {enterprise (slug: $slug) { slug, id } }`
	vars, _ := json.Marshal(map[string]string{"slug": enterpriseName})

	data, err := c.graphql.Post(ctx, query, vars)
	if err != nil {
		return "", fmt.Errorf("failed to get enterprise ID for %q: %w", enterpriseName, err)
	}

	idRaw, err := navigateJSON(data, "enterprise.id")
	if err != nil {
		return "", fmt.Errorf("failed to parse enterprise ID for %q: %w", enterpriseName, err)
	}

	var id string
	if err := json.Unmarshal(idRaw, &id); err != nil {
		return "", fmt.Errorf("failed to unmarshal enterprise ID for %q: %w", enterpriseName, err)
	}
	return id, nil
}

// GetLoginName returns the login name of the authenticated user (viewer).
func (c *Client) GetLoginName(ctx context.Context) (string, error) {
	query := `query{viewer{login}}`

	data, err := c.graphql.Post(ctx, query, nil)
	if err != nil {
		return "", fmt.Errorf("failed to get login name: %w", err)
	}

	loginRaw, err := navigateJSON(data, "viewer.login")
	if err != nil {
		return "", fmt.Errorf("failed to parse login name: %w", err)
	}

	var login string
	if err := json.Unmarshal(loginRaw, &login); err != nil {
		return "", fmt.Errorf("failed to unmarshal login name: %w", err)
	}
	return login, nil
}

// GetUserId returns the node ID for the given user.
func (c *Client) GetUserId(ctx context.Context, login string) (string, error) {
	query := `query($login: String!) {user(login: $login) { id, name } }`
	vars, _ := json.Marshal(map[string]string{"login": login})

	data, err := c.graphql.Post(ctx, query, vars)
	if err != nil {
		return "", fmt.Errorf("failed to get user ID for %q: %w", login, err)
	}

	idRaw, err := navigateJSON(data, "user.id")
	if err != nil {
		return "", fmt.Errorf("failed to parse user ID for %q: %w", login, err)
	}

	var id string
	if err := json.Unmarshal(idRaw, &id); err != nil {
		return "", fmt.Errorf("failed to unmarshal user ID for %q: %w", login, err)
	}
	return id, nil
}

// DoesOrgExist checks whether an organization exists (REST GET /orgs/{org}).
// Returns false when the API returns 404.
func (c *Client) DoesOrgExist(ctx context.Context, org string) (bool, error) {
	_, resp, err := c.rest.Organizations.Get(ctx, org)
	if err != nil {
		if resp != nil && resp.StatusCode == http.StatusNotFound {
			return false, nil
		}
		return false, fmt.Errorf("failed to check if org %q exists: %w", org, err)
	}
	return true, nil
}

// GetOrgMembershipForUser returns the role of a user within an organization.
// Returns "" if the user is not a member (404).
func (c *Client) GetOrgMembershipForUser(ctx context.Context, org, member string) (string, error) {
	membership, resp, err := c.rest.Organizations.GetOrgMembership(ctx, member, org)
	if err != nil {
		if resp != nil && resp.StatusCode == http.StatusNotFound {
			return "", nil
		}
		return "", fmt.Errorf("failed to get membership for %q in %q: %w", member, org, err)
	}
	return membership.GetRole(), nil
}

// ---------------------------------------------------------------------------
// Migration sources & mutations
// ---------------------------------------------------------------------------

// createMigrationSource is the shared implementation for creating ADO, BBS, and GHEC migration sources.
func (c *Client) createMigrationSource(ctx context.Context, name, sourceURL, orgID, sourceType string) (string, error) {
	mutation := `mutation createMigrationSource($name: String!, $url: String!, $ownerId: ID!, $type: MigrationSourceType!) {
		createMigrationSource(input: {name: $name, url: $url, ownerId: $ownerId, type: $type}) {
			migrationSource { id, name, url, type }
		}
	}`
	vars, _ := json.Marshal(map[string]string{
		"name":    name,
		"url":     sourceURL,
		"ownerId": orgID,
		"type":    sourceType,
	})

	data, err := c.graphql.Post(ctx, mutation, vars)
	if err != nil {
		return "", fmt.Errorf("failed to create %s migration source: %w", sourceType, err)
	}

	idRaw, err := navigateJSON(data, "createMigrationSource.migrationSource.id")
	if err != nil {
		return "", fmt.Errorf("failed to parse migration source ID: %w", err)
	}

	var id string
	if err := json.Unmarshal(idRaw, &id); err != nil {
		return "", fmt.Errorf("failed to unmarshal migration source ID: %w", err)
	}
	return id, nil
}

// CreateAdoMigrationSource creates an Azure DevOps migration source.
func (c *Client) CreateAdoMigrationSource(ctx context.Context, orgID string, adoServerURL string) (string, error) {
	sourceURL := adoServerURL
	if sourceURL == "" {
		sourceURL = "https://dev.azure.com"
	}
	return c.createMigrationSource(ctx, "Azure DevOps Source", sourceURL, orgID, "AZURE_DEVOPS")
}

// CreateBbsMigrationSource creates a Bitbucket Server migration source.
func (c *Client) CreateBbsMigrationSource(ctx context.Context, orgID string) (string, error) {
	return c.createMigrationSource(ctx, "Bitbucket Server Source", "https://not-used", orgID, "BITBUCKET_SERVER")
}

// CreateGhecMigrationSource creates a GitHub Enterprise Cloud migration source.
func (c *Client) CreateGhecMigrationSource(ctx context.Context, orgID string) (string, error) {
	return c.createMigrationSource(ctx, "GHEC Source", "https://github.com", orgID, "GITHUB_ARCHIVE")
}

// StartMigration starts a repository migration.
func (c *Client) StartMigration(ctx context.Context, migrationSourceID, sourceRepoURL, orgID, repo, sourceToken, targetToken string, opts ...StartMigrationOption) (string, error) {
	params := &startMigrationParams{}
	for _, o := range opts {
		o(params)
	}

	mutation := `mutation startRepositoryMigration(
		$sourceId: ID!, $ownerId: ID!, $sourceRepositoryUrl: URI!, $repositoryName: String!,
		$continueOnError: Boolean!, $gitArchiveUrl: String, $metadataArchiveUrl: String,
		$accessToken: String!, $githubPat: String, $skipReleases: Boolean,
		$targetRepoVisibility: String, $lockSource: Boolean
	) {
		startRepositoryMigration(input: {
			sourceId: $sourceId, ownerId: $ownerId, sourceRepositoryUrl: $sourceRepositoryUrl,
			repositoryName: $repositoryName, continueOnError: $continueOnError,
			gitArchiveUrl: $gitArchiveUrl, metadataArchiveUrl: $metadataArchiveUrl,
			accessToken: $accessToken, githubPat: $githubPat, skipReleases: $skipReleases,
			targetRepoVisibility: $targetRepoVisibility, lockSource: $lockSource
		}) {
			repositoryMigration { id, databaseId, migrationSource { id, name, type }, sourceUrl, state, failureReason }
		}
	}`

	varsMap := map[string]interface{}{
		"sourceId":             migrationSourceID,
		"ownerId":              orgID,
		"sourceRepositoryUrl":  sourceRepoURL,
		"repositoryName":       repo,
		"continueOnError":      true,
		"accessToken":          sourceToken,
		"githubPat":            targetToken,
		"gitArchiveUrl":        params.gitArchiveURL,
		"metadataArchiveUrl":   params.metadataArchiveURL,
		"skipReleases":         params.skipReleases,
		"targetRepoVisibility": params.targetRepoVisibility,
		"lockSource":           params.lockSource,
	}
	vars, _ := json.Marshal(varsMap)

	data, err := c.graphql.Post(ctx, mutation, vars)
	if err != nil {
		return "", fmt.Errorf("failed to start migration for %q: %w", repo, err)
	}

	idRaw, err := navigateJSON(data, "startRepositoryMigration.repositoryMigration.id")
	if err != nil {
		return "", fmt.Errorf("failed to parse migration ID: %w", err)
	}

	var id string
	if err := json.Unmarshal(idRaw, &id); err != nil {
		return "", fmt.Errorf("failed to unmarshal migration ID: %w", err)
	}
	return id, nil
}

// StartBbsMigration starts a Bitbucket Server migration.
func (c *Client) StartBbsMigration(ctx context.Context, migrationSourceID, bbsRepoURL, orgID, repo, targetToken, archiveURL, targetRepoVisibility string) (string, error) {
	return c.StartMigration(ctx, migrationSourceID, bbsRepoURL, orgID, repo,
		"not-used", targetToken,
		WithGitArchiveURL(archiveURL),
		WithMetadataArchiveURL("https://not-used"),
		WithSkipReleases(false),
		WithTargetRepoVisibility(targetRepoVisibility),
		WithLockSource(false),
	)
}

// StartOrganizationMigration starts an organization-level migration.
func (c *Client) StartOrganizationMigration(ctx context.Context, sourceOrgURL, targetOrgName, targetEnterpriseID, sourceAccessToken string) (string, error) {
	mutation := `mutation startOrganizationMigration(
		$sourceOrgUrl: URI!, $targetOrgName: String!, $targetEnterpriseId: ID!, $sourceAccessToken: String!
	) {
		startOrganizationMigration(input: {
			sourceOrgUrl: $sourceOrgUrl, targetOrgName: $targetOrgName,
			targetEnterpriseId: $targetEnterpriseId, sourceAccessToken: $sourceAccessToken
		}) {
			orgMigration { id, databaseId }
		}
	}`
	vars, _ := json.Marshal(map[string]string{
		"sourceOrgUrl":       sourceOrgURL,
		"targetOrgName":      targetOrgName,
		"targetEnterpriseId": targetEnterpriseID,
		"sourceAccessToken":  sourceAccessToken,
	})

	data, err := c.graphql.Post(ctx, mutation, vars)
	if err != nil {
		return "", fmt.Errorf("failed to start organization migration: %w", err)
	}

	idRaw, err := navigateJSON(data, "startOrganizationMigration.orgMigration.id")
	if err != nil {
		return "", fmt.Errorf("failed to parse org migration ID: %w", err)
	}

	var id string
	if err := json.Unmarshal(idRaw, &id); err != nil {
		return "", fmt.Errorf("failed to unmarshal org migration ID: %w", err)
	}
	return id, nil
}

// GetMigration retrieves migration details by node ID.
func (c *Client) GetMigration(ctx context.Context, migrationID string) (*Migration, error) {
	query := `query($id: ID!) {
		node(id: $id) {
			... on Migration {
				id, sourceUrl, migrationLogUrl, migrationSource { name }, state, warningsCount, failureReason, repositoryName
			}
		}
	}`
	vars, _ := json.Marshal(map[string]string{"id": migrationID})

	data, err := c.graphql.Post(ctx, query, vars)
	if err != nil {
		return nil, fmt.Errorf("failed to get migration %q: %w", migrationID, err)
	}

	nodeRaw, err := navigateJSON(data, "node")
	if err != nil {
		return nil, fmt.Errorf("failed to parse migration %q: %w", migrationID, err)
	}

	var result struct {
		ID              string `json:"id"`
		SourceURL       string `json:"sourceUrl"`
		MigrationLogURL string `json:"migrationLogUrl"`
		MigrationSource struct {
			Name string `json:"name"`
		} `json:"migrationSource"`
		State          string `json:"state"`
		WarningsCount  int    `json:"warningsCount"`
		FailureReason  string `json:"failureReason"`
		RepositoryName string `json:"repositoryName"`
	}
	if err := json.Unmarshal(nodeRaw, &result); err != nil {
		return nil, fmt.Errorf("failed to unmarshal migration %q: %w", migrationID, err)
	}

	return &Migration{
		ID:              result.ID,
		SourceURL:       result.SourceURL,
		MigrationLogURL: result.MigrationLogURL,
		State:           result.State,
		WarningsCount:   result.WarningsCount,
		FailureReason:   result.FailureReason,
		RepositoryName:  result.RepositoryName,
		MigrationSource: MigrationSource{Name: result.MigrationSource.Name},
	}, nil
}

// GetOrganizationMigration retrieves an organization migration by node ID.
func (c *Client) GetOrganizationMigration(ctx context.Context, migrationID string) (*OrgMigration, error) {
	query := `query($id: ID!) {
		node(id: $id) {
			... on OrganizationMigration {
				state, sourceOrgUrl, targetOrgName, failureReason, remainingRepositoriesCount, totalRepositoriesCount
			}
		}
	}`
	vars, _ := json.Marshal(map[string]string{"id": migrationID})

	data, err := c.graphql.Post(ctx, query, vars)
	if err != nil {
		return nil, fmt.Errorf("failed to get organization migration %q: %w", migrationID, err)
	}

	nodeRaw, err := navigateJSON(data, "node")
	if err != nil {
		return nil, fmt.Errorf("failed to parse organization migration %q: %w", migrationID, err)
	}

	var result struct {
		State                      string `json:"state"`
		SourceOrgURL               string `json:"sourceOrgUrl"`
		TargetOrgName              string `json:"targetOrgName"`
		FailureReason              string `json:"failureReason"`
		RemainingRepositoriesCount int    `json:"remainingRepositoriesCount"`
		TotalRepositoriesCount     int    `json:"totalRepositoriesCount"`
	}
	if err := json.Unmarshal(nodeRaw, &result); err != nil {
		return nil, fmt.Errorf("failed to unmarshal organization migration %q: %w", migrationID, err)
	}

	return &OrgMigration{
		State:                      result.State,
		SourceOrgURL:               result.SourceOrgURL,
		TargetOrgName:              result.TargetOrgName,
		FailureReason:              result.FailureReason,
		RemainingRepositoriesCount: result.RemainingRepositoriesCount,
		TotalRepositoriesCount:     result.TotalRepositoriesCount,
	}, nil
}

// GetMigrationLogUrl looks up the migration log URL for the most recent migration
// of a given repository within an organization.
func (c *Client) GetMigrationLogUrl(ctx context.Context, org, repo string) (*MigrationLogResult, error) {
	query := `query($org: String!, $repo: String!) {
		organization(login: $org) {
			repositoryMigrations(last: 1, repositoryName: $repo) {
				nodes { id, migrationLogUrl }
			}
		}
	}`
	vars, _ := json.Marshal(map[string]string{"org": org, "repo": repo})

	data, err := c.graphql.Post(ctx, query, vars)
	if err != nil {
		return nil, fmt.Errorf("failed to get migration log URL for %s/%s: %w", org, repo, err)
	}

	nodesRaw, err := navigateJSON(data, "organization.repositoryMigrations.nodes")
	if err != nil {
		return nil, fmt.Errorf("failed to parse migration log URL response: %w", err)
	}

	var nodes []struct {
		ID              string `json:"id"`
		MigrationLogURL string `json:"migrationLogUrl"`
	}
	if err := json.Unmarshal(nodesRaw, &nodes); err != nil {
		return nil, fmt.Errorf("failed to unmarshal migration log URL response: %w", err)
	}

	if len(nodes) == 0 {
		return &MigrationLogResult{}, nil
	}

	return &MigrationLogResult{
		MigrationLogURL: nodes[0].MigrationLogURL,
		MigrationID:     nodes[0].ID,
	}, nil
}

// AbortMigration aborts a repository migration by ID.
// Returns whether the abort was successful.
func (c *Client) AbortMigration(ctx context.Context, migrationID string) (bool, error) {
	mutation := `mutation abortRepositoryMigration($migrationId: ID!) {
		abortRepositoryMigration(input: { migrationId: $migrationId }) { success }
	}`
	vars, _ := json.Marshal(map[string]string{"migrationId": migrationID})

	data, err := c.graphql.Post(ctx, mutation, vars)
	if err != nil {
		if strings.Contains(err.Error(), "Could not resolve to a node") {
			return false, fmt.Errorf("invalid migration id: %s", migrationID)
		}
		return false, fmt.Errorf("failed to abort migration %q: %w", migrationID, err)
	}

	successRaw, err := navigateJSON(data, "abortRepositoryMigration.success")
	if err != nil {
		return false, fmt.Errorf("failed to parse abort response: %w", err)
	}

	var success bool
	if err := json.Unmarshal(successRaw, &success); err != nil {
		return false, fmt.Errorf("failed to unmarshal abort response: %w", err)
	}
	return success, nil
}

// GrantMigratorRole grants the migrator role to an actor within an organization.
func (c *Client) GrantMigratorRole(ctx context.Context, orgID, actor, actorType string) (bool, error) {
	mutation := `mutation grantMigratorRole($organizationId: ID!, $actor: String!, $actor_type: ActorType!) {
		grantMigratorRole(input: { organizationId: $organizationId, actor: $actor, actorType: $actor_type }) { success }
	}`
	vars, _ := json.Marshal(map[string]string{
		"organizationId": orgID,
		"actor":          actor,
		"actor_type":     actorType,
	})

	data, err := c.graphql.Post(ctx, mutation, vars)
	if err != nil {
		// C# catches HttpRequestException and returns false
		c.logger.Warning("Failed to grant migrator role for %q in org: %v", actor, err)
		return false, nil //nolint:nilerr
	}

	successRaw, err := navigateJSON(data, "grantMigratorRole.success")
	if err != nil {
		c.logger.Warning("Failed to parse grant migrator role response: %v", err)
		return false, nil
	}

	var success bool
	if err := json.Unmarshal(successRaw, &success); err != nil {
		c.logger.Warning("Failed to unmarshal grant migrator role response: %v", err)
		return false, nil
	}
	return success, nil
}

// RevokeMigratorRole revokes the migrator role from an actor within an organization.
func (c *Client) RevokeMigratorRole(ctx context.Context, orgID, actor, actorType string) (bool, error) {
	mutation := `mutation revokeMigratorRole($organizationId: ID!, $actor: String!, $actor_type: ActorType!) {
		revokeMigratorRole(input: { organizationId: $organizationId, actor: $actor, actorType: $actor_type }) { success }
	}`
	vars, _ := json.Marshal(map[string]string{
		"organizationId": orgID,
		"actor":          actor,
		"actor_type":     actorType,
	})

	data, err := c.graphql.Post(ctx, mutation, vars)
	if err != nil {
		// C# catches HttpRequestException and returns false
		c.logger.Warning("Failed to revoke migrator role for %q in org: %v", actor, err)
		return false, nil //nolint:nilerr
	}

	successRaw, err := navigateJSON(data, "revokeMigratorRole.success")
	if err != nil {
		c.logger.Warning("Failed to parse revoke migrator role response: %v", err)
		return false, nil
	}

	var success bool
	if err := json.Unmarshal(successRaw, &success); err != nil {
		c.logger.Warning("Failed to unmarshal revoke migrator role response: %v", err)
		return false, nil
	}
	return success, nil
}

// ---------------------------------------------------------------------------
// Team methods
// ---------------------------------------------------------------------------

// CreateTeam creates a team in the given organization with "closed" privacy.
// On 5xx errors, it checks whether the team was already created (idempotency).
func (c *Client) CreateTeam(ctx context.Context, org, teamName string) (*Team, error) {
	team, resp, err := c.rest.Teams.CreateTeam(ctx, org, gogithub.NewTeam{
		Name:    teamName,
		Privacy: gogithub.Ptr("closed"),
	})
	if err != nil {
		// On 5xx, check if team was actually created (idempotency)
		if resp != nil && resp.StatusCode >= 500 {
			teams, listErr := c.GetTeams(ctx, org)
			if listErr == nil {
				for _, t := range teams {
					if strings.EqualFold(t.Name, teamName) {
						return &t, nil
					}
				}
			}
		}
		return nil, fmt.Errorf("failed to create team %q in %q: %w", teamName, org, err)
	}

	return &Team{
		ID:   fmt.Sprintf("%d", team.GetID()),
		Name: team.GetName(),
		Slug: team.GetSlug(),
	}, nil
}

// GetTeams lists all teams in an organization.
func (c *Client) GetTeams(ctx context.Context, org string) ([]Team, error) {
	var allTeams []Team
	opts := &gogithub.ListOptions{PerPage: 100}

	for {
		teams, resp, err := c.rest.Teams.ListTeams(ctx, org, opts)
		if err != nil {
			return nil, fmt.Errorf("failed to list teams for %q: %w", org, err)
		}

		for _, t := range teams {
			allTeams = append(allTeams, Team{
				ID:   fmt.Sprintf("%d", t.GetID()),
				Name: t.GetName(),
				Slug: t.GetSlug(),
			})
		}

		if resp.NextPage == 0 {
			break
		}
		opts.Page = resp.NextPage
	}

	return allTeams, nil
}

// GetTeamMembers lists the members of a team by slug.
func (c *Client) GetTeamMembers(ctx context.Context, org, teamSlug string) ([]string, error) {
	var members []string
	opts := &gogithub.TeamListTeamMembersOptions{
		ListOptions: gogithub.ListOptions{PerPage: 100},
	}

	for {
		users, resp, err := c.rest.Teams.ListTeamMembersBySlug(ctx, org, teamSlug, opts)
		if err != nil {
			return nil, fmt.Errorf("failed to list team members for %q/%q: %w", org, teamSlug, err)
		}

		for _, u := range users {
			members = append(members, u.GetLogin())
		}

		if resp.NextPage == 0 {
			break
		}
		opts.Page = resp.NextPage
	}

	return members, nil
}

// RemoveTeamMember removes a user from a team.
func (c *Client) RemoveTeamMember(ctx context.Context, org, teamSlug, member string) error {
	_, err := c.rest.Teams.RemoveTeamMembershipBySlug(ctx, org, teamSlug, member)
	if err != nil {
		return fmt.Errorf("failed to remove %q from team %q/%q: %w", member, org, teamSlug, err)
	}
	return nil
}

// GetTeamSlug finds a team by name (case-insensitive) and returns its slug.
func (c *Client) GetTeamSlug(ctx context.Context, org, teamName string) (string, error) {
	teams, err := c.GetTeams(ctx, org)
	if err != nil {
		return "", err
	}

	for _, t := range teams {
		if strings.EqualFold(t.Name, teamName) {
			return t.Slug, nil
		}
	}

	return "", fmt.Errorf("team %q not found in organization %q", teamName, org)
}

// AddTeamSync sets up team sync group mappings for a team.
func (c *Client) AddTeamSync(ctx context.Context, org, teamSlug, groupID, groupName, groupDescription string) error {
	payload := map[string]interface{}{
		"groups": []map[string]string{
			{
				"group_id":          groupID,
				"group_name":        groupName,
				"group_description": groupDescription,
			},
		},
	}

	u := fmt.Sprintf("orgs/%s/teams/%s/team-sync/group-mappings", org, teamSlug)
	req, err := c.rest.NewRequest("PATCH", u, payload)
	if err != nil {
		return fmt.Errorf("failed to create team sync request: %w", err)
	}

	_, err = c.rest.Do(ctx, req, nil)
	if err != nil {
		return fmt.Errorf("failed to add team sync for %q/%q: %w", org, teamSlug, err)
	}
	return nil
}

// AddTeamToRepo adds a team to a repository with the given permission role.
func (c *Client) AddTeamToRepo(ctx context.Context, org, teamSlug, repo, role string) error {
	opts := &gogithub.TeamAddTeamRepoOptions{Permission: role}
	_, err := c.rest.Teams.AddTeamRepoBySlug(ctx, org, teamSlug, org, repo, opts)
	if err != nil {
		return fmt.Errorf("failed to add team %q to repo %s/%s: %w", teamSlug, org, repo, err)
	}
	return nil
}

// GetIdpGroupId looks up the external group ID for a given group name (case-insensitive).
func (c *Client) GetIdpGroupId(ctx context.Context, org, groupName string) (int, error) {
	u := fmt.Sprintf("orgs/%s/external-groups", org)

	req, err := c.rest.NewRequest("GET", u, nil)
	if err != nil {
		return 0, fmt.Errorf("failed to create external groups request: %w", err)
	}

	var result struct {
		Groups []struct {
			GroupID   int    `json:"group_id"`
			GroupName string `json:"group_name"`
		} `json:"groups"`
	}
	_, err = c.rest.Do(ctx, req, &result)
	if err != nil {
		return 0, fmt.Errorf("failed to get external groups for %q: %w", org, err)
	}

	for _, g := range result.Groups {
		if strings.EqualFold(g.GroupName, groupName) {
			return g.GroupID, nil
		}
	}

	return 0, fmt.Errorf("external group %q not found in organization %q", groupName, org)
}

// AddEmuGroupToTeam links an EMU external group to a team.
func (c *Client) AddEmuGroupToTeam(ctx context.Context, org, teamSlug string, groupID int) error {
	payload := map[string]int{"group_id": groupID}

	u := fmt.Sprintf("orgs/%s/teams/%s/external-groups", org, teamSlug)
	req, err := c.rest.NewRequest("PATCH", u, payload)
	if err != nil {
		return fmt.Errorf("failed to create EMU group request: %w", err)
	}

	_, err = c.rest.Do(ctx, req, nil)
	if err != nil {
		return fmt.Errorf("failed to add EMU group %d to team %q/%q: %w", groupID, org, teamSlug, err)
	}
	return nil
}

// ---------------------------------------------------------------------------
// Mannequin methods
// ---------------------------------------------------------------------------

// GetMannequins retrieves all mannequins for an organization (by org node ID).
func (c *Client) GetMannequins(ctx context.Context, orgID string) ([]Mannequin, error) {
	query := `query($id: ID!, $first: Int, $after: String) {
		node(id: $id) {
			... on Organization {
				mannequins(first: $first, after: $after) {
					pageInfo { endCursor, hasNextPage }
					nodes { login, id, claimant { login, id } }
				}
			}
		}
	}`
	vars, _ := json.Marshal(map[string]string{"id": orgID})

	data, err := c.graphql.PostWithPagination(ctx, query, vars,
		"node.mannequins.nodes", "node.mannequins.pageInfo")
	if err != nil {
		return nil, fmt.Errorf("failed to get mannequins for org %q: %w", orgID, err)
	}

	return parseMannequins(data)
}

// GetMannequinsByLogin retrieves mannequins for an organization filtered by login.
func (c *Client) GetMannequinsByLogin(ctx context.Context, orgID, login string) ([]Mannequin, error) {
	query := `query($id: ID!, $first: Int, $after: String, $login: String) {
		node(id: $id) {
			... on Organization {
				mannequins(first: $first, after: $after, login: $login) {
					pageInfo { endCursor, hasNextPage }
					nodes { login, id, claimant { login, id } }
				}
			}
		}
	}`
	vars, _ := json.Marshal(map[string]interface{}{
		"id":    orgID,
		"login": login,
	})

	data, err := c.graphql.PostWithPagination(ctx, query, vars,
		"node.mannequins.nodes", "node.mannequins.pageInfo")
	if err != nil {
		return nil, fmt.Errorf("failed to get mannequins by login %q for org %q: %w", login, orgID, err)
	}

	return parseMannequins(data)
}

// parseMannequins converts raw JSON mannequin nodes into Mannequin structs.
func parseMannequins(data json.RawMessage) ([]Mannequin, error) {
	var nodes []struct {
		Login    string `json:"login"`
		ID       string `json:"id"`
		Claimant *struct {
			Login string `json:"login"`
			ID    string `json:"id"`
		} `json:"claimant"`
	}
	if err := json.Unmarshal(data, &nodes); err != nil {
		return nil, fmt.Errorf("failed to unmarshal mannequins: %w", err)
	}

	mannequins := make([]Mannequin, 0, len(nodes))
	for _, n := range nodes {
		m := Mannequin{
			ID:    n.ID,
			Login: n.Login,
		}
		if n.Claimant != nil {
			m.MappedUser = &MannequinUser{
				ID:    n.Claimant.ID,
				Login: n.Claimant.Login,
			}
		}
		mannequins = append(mannequins, m)
	}
	return mannequins, nil
}

// CreateAttributionInvitation creates an attribution invitation to map a mannequin to a user.
func (c *Client) CreateAttributionInvitation(ctx context.Context, orgID, sourceID, targetID string) (*CreateAttributionInvitationResult, error) {
	mutation := `mutation($orgId: ID!, $sourceId: ID!, $targetId: ID!) {
		createAttributionInvitation(input: { ownerId: $orgId, sourceId: $sourceId, targetId: $targetId }) {
			source { ... on Mannequin { id, login } }
			target { ... on User { id, login } }
		}
	}`
	vars, _ := json.Marshal(map[string]string{
		"orgId":    orgID,
		"sourceId": sourceID,
		"targetId": targetID,
	})

	// Use raw Post to capture potential errors in the response body
	data, err := c.graphql.Post(ctx, mutation, vars)
	if err != nil {
		return nil, fmt.Errorf("failed to create attribution invitation: %w", err)
	}

	invRaw, err := navigateJSON(data, "createAttributionInvitation")
	if err != nil {
		return nil, fmt.Errorf("failed to parse attribution invitation response: %w", err)
	}

	var result CreateAttributionInvitationResult
	if err := json.Unmarshal(invRaw, &result); err != nil {
		return nil, fmt.Errorf("failed to unmarshal attribution invitation response: %w", err)
	}
	return &result, nil
}

// ReclaimMannequinSkipInvitation reclaims a mannequin, skipping the email invitation.
func (c *Client) ReclaimMannequinSkipInvitation(ctx context.Context, orgID, sourceID, targetID string) (*ReattributeMannequinToUserResult, error) {
	mutation := `mutation($orgId: ID!, $sourceId: ID!, $targetId: ID!) {
		reattributeMannequinToUser(input: { ownerId: $orgId, sourceId: $sourceId, targetId: $targetId }) {
			source { ... on Mannequin { id, login } }
			target { ... on User { id, login } }
		}
	}`
	vars, _ := json.Marshal(map[string]string{
		"orgId":    orgID,
		"sourceId": sourceID,
		"targetId": targetID,
	})

	data, err := c.graphql.Post(ctx, mutation, vars)
	if err != nil {
		errStr := err.Error()
		if strings.Contains(errStr, "Field 'reattributeMannequinToUser' doesn't exist on type 'Mutation'") {
			return nil, fmt.Errorf("reclaim mannequin (skip invitation) is not available for this GitHub product. Error: %w", err)
		}
		if strings.Contains(errStr, "Target must be a member") {
			return &ReattributeMannequinToUserResult{
				Errors: []ErrorData{{Message: errStr}},
			}, nil
		}
		return nil, fmt.Errorf("failed to reclaim mannequin: %w", err)
	}

	resultRaw, err := navigateJSON(data, "reattributeMannequinToUser")
	if err != nil {
		return nil, fmt.Errorf("failed to parse reclaim mannequin response: %w", err)
	}

	var result ReattributeMannequinToUserResult
	if err := json.Unmarshal(resultRaw, &result); err != nil {
		return nil, fmt.Errorf("failed to unmarshal reclaim mannequin response: %w", err)
	}
	return &result, nil
}
