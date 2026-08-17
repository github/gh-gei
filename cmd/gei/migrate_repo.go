package main

import (
	"context"
	"fmt"
	"io"
	"net/url"
	"os"
	"regexp"
	"strings"
	"time"

	"github.com/github/gh-gei/internal/cmdutil"
	"github.com/github/gh-gei/pkg/archive"
	"github.com/github/gh-gei/pkg/download"
	"github.com/github/gh-gei/pkg/env"
	"github.com/github/gh-gei/pkg/filesystem"
	"github.com/github/gh-gei/pkg/github"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/github/gh-gei/pkg/migration"
	"github.com/spf13/cobra"
)

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

const (
	archiveGenerationTimeoutDefault = 20 * time.Hour
	archivePollIntervalDefault      = 10 * time.Second
	migrationPollIntervalDefault    = 10 * time.Second
	gitArchiveFileName              = "git_archive.tar.gz"
	metadataArchiveFileName         = "metadata_archive.tar.gz"
	duplicateArchiveFileName        = "archive.tar.gz"
	defaultGitHubBaseURL            = "https://github.com"
	defaultGitHubAPIURL             = "https://api.github.com"
)

// ---------------------------------------------------------------------------
// Consumer-defined interfaces
// ---------------------------------------------------------------------------

// migrateRepoSourceGitHub defines methods needed from the source GitHub API.
type migrateRepoSourceGitHub interface {
	StartGitArchiveGeneration(ctx context.Context, org, repo string) (int, error)
	StartMetadataArchiveGeneration(ctx context.Context, org, repo string, skipReleases, lockSource bool) (int, error)
	GetArchiveMigrationStatus(ctx context.Context, org string, archiveID int) (string, error)
	GetArchiveMigrationUrl(ctx context.Context, org string, archiveID int) (string, error)
}

// migrateRepoTargetGitHub defines methods needed from the target GitHub API.
type migrateRepoTargetGitHub interface {
	DoesRepoExist(ctx context.Context, org, repo string) (bool, error)
	DoesOrgExist(ctx context.Context, org string) (bool, error)
	GetOrganizationId(ctx context.Context, org string) (string, error)
	CreateGhecMigrationSource(ctx context.Context, orgID string) (string, error)
	StartMigration(ctx context.Context, migrationSourceID, sourceRepoURL, orgID, repo, sourceToken, targetToken string, opts ...github.StartMigrationOption) (string, error)
	GetMigration(ctx context.Context, id string) (*github.Migration, error)
}

// migrateRepoVersionFetcher fetches the GHES version.
type migrateRepoVersionFetcher interface {
	GetVersion(ctx context.Context) (*github.VersionInfo, error)
}

// migrateRepoEnvProvider provides environment variable fallbacks.
type migrateRepoEnvProvider interface {
	SourceGitHubPAT() string
	TargetGitHubPAT() string
	AzureStorageConnectionString() string
	AWSAccessKeyID() string
	AWSSecretAccessKey() string
	AWSSessionToken() string
	AWSRegion() string
}

// migrateRepoArchiveUploader uploads archives to blob storage.
type migrateRepoArchiveUploader interface {
	Upload(ctx context.Context, targetOrg, fileName string, content io.ReadSeeker, size int64) (string, error)
}

// migrateRepoHTTPDownloader downloads files over HTTP.
type migrateRepoHTTPDownloader interface {
	DownloadToFile(ctx context.Context, url, destPath string) error
}

// migrateRepoFileSystem provides filesystem operations.
type migrateRepoFileSystem interface {
	GetTempFileName() string
	OpenRead(path string) (io.ReadSeekCloser, int64, error)
	DeleteIfExists(path string) error
	FileExists(path string) bool
}

// io.ReadSeekCloser is not in stdlib, so define it:
// Actually it is in io package since Go 1.16. Let's use it.

// ---------------------------------------------------------------------------
// Options (configurable for testing)
// ---------------------------------------------------------------------------

type migrateRepoOptions struct {
	pollInterval        time.Duration
	archivePollInterval time.Duration
	archiveTimeout      time.Duration
}

// ---------------------------------------------------------------------------
// Command constructor
// ---------------------------------------------------------------------------

func newMigrateRepoCmd(
	sourceGH migrateRepoSourceGitHub,
	targetGH migrateRepoTargetGitHub,
	envProv migrateRepoEnvProvider,
	uploader migrateRepoArchiveUploader,
	downloader migrateRepoHTTPDownloader,
	fs migrateRepoFileSystem,
	log *logger.Logger,
	opts migrateRepoOptions,
) *cobra.Command {
	var (
		githubSourceOrg              string
		sourceRepo                   string
		githubTargetOrg              string
		targetRepo                   string
		targetAPIURL                 string
		targetUploadsURL             string
		ghesAPIURL                   string
		azureStorageConnectionString string
		awsBucketName                string
		awsAccessKey                 string
		awsSecretKey                 string
		awsSessionToken              string
		awsRegion                    string
		noSSLVerify                  bool
		gitArchiveURL                string
		metadataArchiveURL           string
		gitArchivePath               string
		metadataArchivePath          string
		skipReleases                 bool
		lockSourceRepo               bool
		queueOnly                    bool
		targetRepoVisibility         string
		githubSourcePAT              string
		githubTargetPAT              string
		keepArchive                  bool
		useGithubStorage             bool
	)

	cmd := &cobra.Command{
		Use:   "migrate-repo",
		Short: "Migrates a repository to GitHub using GitHub Enterprise Importer",
		Long:  "Migrates a repository from GitHub or GitHub Enterprise Server to GitHub.com using GitHub Enterprise Importer.",
		RunE: func(cmd *cobra.Command, args []string) error {
			return runMigrateRepo(cmd.Context(), migrateRepoArgs{
				githubSourceOrg:              githubSourceOrg,
				sourceRepo:                   sourceRepo,
				githubTargetOrg:              githubTargetOrg,
				targetRepo:                   targetRepo,
				targetAPIURL:                 targetAPIURL,
				targetUploadsURL:             targetUploadsURL,
				ghesAPIURL:                   ghesAPIURL,
				azureStorageConnectionString: azureStorageConnectionString,
				awsBucketName:                awsBucketName,
				awsAccessKey:                 awsAccessKey,
				awsSecretKey:                 awsSecretKey,
				awsSessionToken:              awsSessionToken,
				awsRegion:                    awsRegion,
				noSSLVerify:                  noSSLVerify,
				gitArchiveURL:                gitArchiveURL,
				metadataArchiveURL:           metadataArchiveURL,
				gitArchivePath:               gitArchivePath,
				metadataArchivePath:          metadataArchivePath,
				skipReleases:                 skipReleases,
				lockSourceRepo:               lockSourceRepo,
				queueOnly:                    queueOnly,
				targetRepoVisibility:         targetRepoVisibility,
				githubSourcePAT:              githubSourcePAT,
				githubTargetPAT:              githubTargetPAT,
				keepArchive:                  keepArchive,
				useGithubStorage:             useGithubStorage,
			}, sourceGH, targetGH, envProv, uploader, downloader, fs, log, opts)
		},
	}

	// Required flags
	cmd.Flags().StringVar(&githubSourceOrg, "github-source-org", "", "Source GitHub organization (REQUIRED)")
	cmd.Flags().StringVar(&sourceRepo, "source-repo", "", "Source repository name (REQUIRED)")
	cmd.Flags().StringVar(&githubTargetOrg, "github-target-org", "", "Target GitHub organization (REQUIRED)")

	// Optional flags
	cmd.Flags().StringVar(&targetRepo, "target-repo", "", "Target repository name (defaults to source-repo)")
	cmd.Flags().StringVar(&targetAPIURL, "target-api-url", "", "API URL for the target GitHub instance (defaults to https://api.github.com)")
	cmd.Flags().StringVar(&targetUploadsURL, "target-uploads-url", "", "Uploads URL for the target GitHub instance")
	cmd.Flags().StringVar(&ghesAPIURL, "ghes-api-url", "", "API endpoint for GHES instance")
	cmd.Flags().StringVar(&azureStorageConnectionString, "azure-storage-connection-string", "", "Azure Blob Storage connection string")
	cmd.Flags().StringVar(&awsBucketName, "aws-bucket-name", "", "AWS S3 bucket name")
	cmd.Flags().StringVar(&awsAccessKey, "aws-access-key", "", "AWS access key (falls back to AWS_ACCESS_KEY_ID env)")
	cmd.Flags().StringVar(&awsSecretKey, "aws-secret-key", "", "AWS secret key (falls back to AWS_SECRET_ACCESS_KEY env)")
	cmd.Flags().StringVar(&awsSessionToken, "aws-session-token", "", "AWS session token (falls back to AWS_SESSION_TOKEN env)")
	cmd.Flags().StringVar(&awsRegion, "aws-region", "", "AWS region (falls back to AWS_REGION env)")
	cmd.Flags().BoolVar(&noSSLVerify, "no-ssl-verify", false, "Disable SSL verification for GHES")
	cmd.Flags().StringVar(&gitArchiveURL, "git-archive-url", "", "URL for pre-generated git archive")
	cmd.Flags().StringVar(&metadataArchiveURL, "metadata-archive-url", "", "URL for pre-generated metadata archive")
	cmd.Flags().StringVar(&gitArchivePath, "git-archive-path", "", "Path to local git archive file")
	cmd.Flags().StringVar(&metadataArchivePath, "metadata-archive-path", "", "Path to local metadata archive file")
	cmd.Flags().BoolVar(&skipReleases, "skip-releases", false, "Skip releases when migrating")
	cmd.Flags().BoolVar(&lockSourceRepo, "lock-source-repo", false, "Lock source repository during migration")
	cmd.Flags().BoolVar(&queueOnly, "queue-only", false, "Queue the migration without waiting for completion")
	cmd.Flags().StringVar(&targetRepoVisibility, "target-repo-visibility", "", "Target repository visibility (public, private, internal)")
	cmd.Flags().StringVar(&githubSourcePAT, "github-source-pat", "", "Personal access token for the source GitHub instance")
	cmd.Flags().StringVar(&githubTargetPAT, "github-target-pat", "", "Personal access token for the target GitHub instance")
	cmd.Flags().BoolVar(&keepArchive, "keep-archive", false, "Keep downloaded archive files after upload")
	cmd.Flags().BoolVar(&useGithubStorage, "use-github-storage", false, "Use GitHub-owned storage for archives")

	// Hidden flags
	_ = cmd.Flags().MarkHidden("git-archive-url")
	_ = cmd.Flags().MarkHidden("metadata-archive-url")
	_ = cmd.Flags().MarkHidden("git-archive-path")
	_ = cmd.Flags().MarkHidden("metadata-archive-path")
	_ = cmd.Flags().MarkHidden("target-uploads-url")
	_ = cmd.Flags().MarkHidden("use-github-storage")

	return cmd
}

// ---------------------------------------------------------------------------
// Args struct
// ---------------------------------------------------------------------------

type migrateRepoArgs struct {
	githubSourceOrg              string
	sourceRepo                   string
	githubTargetOrg              string
	targetRepo                   string
	targetAPIURL                 string
	targetUploadsURL             string
	ghesAPIURL                   string
	azureStorageConnectionString string
	awsBucketName                string
	awsAccessKey                 string
	awsSecretKey                 string
	awsSessionToken              string
	awsRegion                    string
	noSSLVerify                  bool
	gitArchiveURL                string
	metadataArchiveURL           string
	gitArchivePath               string
	metadataArchivePath          string
	skipReleases                 bool
	lockSourceRepo               bool
	queueOnly                    bool
	targetRepoVisibility         string
	githubSourcePAT              string
	githubTargetPAT              string
	keepArchive                  bool
	useGithubStorage             bool
}

// ---------------------------------------------------------------------------
// Validation
// ---------------------------------------------------------------------------

func validateMigrateRepoArgs(a *migrateRepoArgs, envProv migrateRepoEnvProvider, log *logger.Logger) error {
	// Required fields
	if err := cmdutil.ValidateRequired(a.githubSourceOrg, "--github-source-org"); err != nil {
		return err
	}
	if err := cmdutil.ValidateRequired(a.sourceRepo, "--source-repo"); err != nil {
		return err
	}
	if err := cmdutil.ValidateRequired(a.githubTargetOrg, "--github-target-org"); err != nil {
		return err
	}

	// No URL validation
	if err := cmdutil.ValidateNoURL(a.githubSourceOrg, "--github-source-org"); err != nil {
		return err
	}
	if err := cmdutil.ValidateNoURL(a.githubTargetOrg, "--github-target-org"); err != nil {
		return err
	}
	if err := cmdutil.ValidateNoURL(a.sourceRepo, "--source-repo"); err != nil {
		return err
	}
	if err := cmdutil.ValidateNoURL(a.targetRepo, "--target-repo"); err != nil {
		return err
	}

	// Default target-repo to source-repo
	if strings.TrimSpace(a.targetRepo) == "" {
		log.Info("Target repo name not provided, defaulting to same as source repo (%s)", a.sourceRepo)
		a.targetRepo = a.sourceRepo
	}

	// Default source PAT to target PAT
	if a.githubTargetPAT != "" && a.githubSourcePAT == "" {
		a.githubSourcePAT = a.githubTargetPAT
		log.Info("Since github-target-pat is provided, github-source-pat will also use its value.")
	}

	// Mutually exclusive archive options
	if a.gitArchiveURL != "" && a.gitArchivePath != "" {
		return cmdutil.NewUserError("The options --git-archive-url and --git-archive-path may not be used together")
	}
	if a.metadataArchiveURL != "" && a.metadataArchivePath != "" {
		return cmdutil.NewUserError("The options --metadata-archive-url and --metadata-archive-path may not be used together")
	}

	// Paired archive options
	gitURLSet := a.gitArchiveURL != ""
	metaURLSet := a.metadataArchiveURL != ""
	if gitURLSet != metaURLSet {
		return cmdutil.NewUserError("When using archive urls, you must provide both --git-archive-url --metadata-archive-url")
	}

	gitPathSet := a.gitArchivePath != ""
	metaPathSet := a.metadataArchivePath != ""
	if gitPathSet != metaPathSet {
		return cmdutil.NewUserError("When using archive files, you must provide both --git-archive-path --metadata-archive-path")
	}

	// GHES-only flags
	ghesMode := strings.TrimSpace(a.ghesAPIURL) != ""
	if !ghesMode {
		if a.awsBucketName != "" && !gitPathSet {
			return cmdutil.NewUserError("When using --aws-bucket-name, you must provide --ghes-api-url, or --git-archive-path and --metadata-archive-path")
		}
		if a.useGithubStorage && !gitPathSet {
			return cmdutil.NewUserError("When using --use-github-storage, you must provide --ghes-api-url, or --git-archive-path and --metadata-archive-path")
		}
		if a.noSSLVerify {
			return cmdutil.NewUserError("--ghes-api-url must be specified when --no-ssl-verify is specified.")
		}
		if a.keepArchive {
			return cmdutil.NewUserError("--ghes-api-url must be specified when --keep-archive is specified.")
		}
	}

	// Storage conflicts
	if a.awsBucketName != "" && a.useGithubStorage {
		return cmdutil.NewUserError("The --use-github-storage flag was provided with an AWS S3 Bucket name. Archive cannot be uploaded to both locations.")
	}
	if a.azureStorageConnectionString != "" && a.useGithubStorage {
		return cmdutil.NewUserError("The --use-github-storage flag was provided with a connection string for an Azure storage account. Archive cannot be uploaded to both locations.")
	}

	// Target repo visibility
	if err := cmdutil.ValidateOneOf(a.targetRepoVisibility, "--target-repo-visibility", "public", "private", "internal"); err != nil {
		return err
	}

	return nil
}

// ---------------------------------------------------------------------------
// Runner
// ---------------------------------------------------------------------------

func runMigrateRepo(
	ctx context.Context,
	a migrateRepoArgs,
	sourceGH migrateRepoSourceGitHub,
	targetGH migrateRepoTargetGitHub,
	envProv migrateRepoEnvProvider,
	uploader migrateRepoArchiveUploader,
	downloader migrateRepoHTTPDownloader,
	fs migrateRepoFileSystem,
	log *logger.Logger,
	opts migrateRepoOptions,
) error {
	if err := validateMigrateRepoArgs(&a, envProv, log); err != nil {
		return err
	}

	log.Info("Migrating Repo...")

	// Determine if blob credentials are required
	blobCredentialsRequired := a.gitArchivePath != ""
	if !blobCredentialsRequired && strings.TrimSpace(a.ghesAPIURL) != "" {
		// Need to check GHES version
		if vf, ok := sourceGH.(migrateRepoVersionFetcher); ok {
			required, err := areBlobCredentialsRequired(ctx, vf, a.ghesAPIURL, log)
			if err != nil {
				return err
			}
			blobCredentialsRequired = required
		}
	}

	ghesMode := strings.TrimSpace(a.ghesAPIURL) != ""

	// Validate upload options for GHES or archive path flows
	if ghesMode || a.gitArchivePath != "" {
		if err := validateUploadOptions(&a, envProv, blobCredentialsRequired, log); err != nil {
			return err
		}
	}

	// GHES pre-checks
	if ghesMode {
		exists, err := targetGH.DoesRepoExist(ctx, a.githubTargetOrg, a.targetRepo)
		if err != nil {
			return err
		}
		if exists {
			return cmdutil.NewUserErrorf("A repository called %s/%s already exists", a.githubTargetOrg, a.targetRepo)
		}

		orgExists, err := targetGH.DoesOrgExist(ctx, a.githubTargetOrg)
		if err != nil {
			return err
		}
		if !orgExists {
			return cmdutil.NewUserErrorf("The target org %q does not exist.", a.githubTargetOrg)
		}
	}

	// Get org ID and create migration source
	githubOrgID, err := targetGH.GetOrganizationId(ctx, a.githubTargetOrg)
	if err != nil {
		return err
	}

	migrationSourceID, err := targetGH.CreateGhecMigrationSource(ctx, githubOrgID)
	if err != nil {
		if strings.Contains(err.Error(), "not have the correct permissions to execute") {
			msg := fmt.Sprintf("%s%s", err.Error(), insufficientPermissionsMessage(a.githubTargetOrg))
			return cmdutil.NewUserError(msg)
		}
		return err
	}

	// GHES archive generation + upload
	if ghesMode {
		gitURL, metaURL, err := generateAndUploadArchives(ctx, sourceGH, targetGH, uploader, downloader, fs, log, &a, blobCredentialsRequired, opts)
		if err != nil {
			return err
		}
		a.gitArchiveURL = gitURL
		a.metadataArchiveURL = metaURL

		if a.useGithubStorage || blobCredentialsRequired {
			log.Info("Archives uploaded to blob storage, now starting migration...")
		}
	} else if a.gitArchivePath != "" && a.metadataArchivePath != "" {
		// Local archive path upload
		gitURL, metaURL, err := uploadLocalArchives(ctx, uploader, fs, log, &a)
		if err != nil {
			return err
		}
		a.gitArchiveURL = gitURL
		a.metadataArchiveURL = metaURL
		log.Info("Archive(s) uploaded to blob storage, now starting migration...")
	}

	// Build source repo URL
	sourceRepoURL := buildSourceRepoURL(a.githubSourceOrg, a.sourceRepo, a.ghesAPIURL)

	// Resolve tokens
	sourceToken := resolveSourceToken(a.githubSourcePAT, envProv)
	targetToken := resolveTargetToken(a.githubTargetPAT, envProv)

	// Build migration options
	var migOpts []github.StartMigrationOption
	if a.gitArchiveURL != "" {
		migOpts = append(migOpts, github.WithGitArchiveURL(a.gitArchiveURL))
	}
	if a.metadataArchiveURL != "" {
		migOpts = append(migOpts, github.WithMetadataArchiveURL(a.metadataArchiveURL))
	}
	if a.skipReleases {
		migOpts = append(migOpts, github.WithSkipReleases(true))
	}
	if a.targetRepoVisibility != "" {
		migOpts = append(migOpts, github.WithTargetRepoVisibility(a.targetRepoVisibility))
	}
	// Lock source only for github.com (not GHES — GHES uses lockSource on archive generation)
	if !ghesMode && a.lockSourceRepo {
		migOpts = append(migOpts, github.WithLockSource(true))
	}

	// Start migration
	migrationID, err := targetGH.StartMigration(ctx, migrationSourceID, sourceRepoURL, githubOrgID, a.targetRepo, sourceToken, targetToken, migOpts...)
	if err != nil {
		// Handle "already exists" gracefully
		if strings.Contains(err.Error(), fmt.Sprintf("A repository called %s/%s already exists", a.githubTargetOrg, a.targetRepo)) {
			log.Warning("The Org '%s' already contains a repository with the name '%s'. No operation will be performed", a.githubTargetOrg, a.targetRepo)
			return nil
		}
		return err
	}

	// Queue-only mode
	if a.queueOnly {
		log.Info("A repository migration (ID: %s) was successfully queued.", migrationID)
		return nil
	}

	// Poll for migration completion
	pollInterval := opts.pollInterval

	m, err := targetGH.GetMigration(ctx, migrationID)
	if err != nil {
		return err
	}

	for migration.IsRepoPending(m.State) {
		log.Info("Migration in progress (ID: %s). State: %s. Waiting %s...", migrationID, m.State, formatPollInterval(pollInterval))

		select {
		case <-ctx.Done():
			return ctx.Err()
		case <-time.After(pollInterval):
		}

		m, err = targetGH.GetMigration(ctx, migrationID)
		if err != nil {
			return err
		}
	}

	if migration.IsRepoFailed(m.State) {
		log.Errorf("Migration Failed. Migration ID: %s", migrationID)
		logWarningsCount(log, m.WarningsCount)
		log.Info("Migration log available at %s or by running `gh gei download-logs --github-target-org %s --target-repo %s`", m.MigrationLogURL, a.githubTargetOrg, a.targetRepo)
		return cmdutil.NewUserError(m.FailureReason)
	}

	log.Success("Migration completed (ID: %s)! State: %s", migrationID, m.State)
	logWarningsCount(log, m.WarningsCount)
	log.Info("Migration log available at %s or by running `gh gei download-logs --github-target-org %s --target-repo %s`", m.MigrationLogURL, a.githubTargetOrg, a.targetRepo)

	return nil
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

func insufficientPermissionsMessage(org string) string {
	return fmt.Sprintf(". Please check that:\n  (a) you are a member of the `%s` organization,\n  (b) you are an organization owner or you have been granted the migrator role and\n  (c) your personal access token has the correct scopes.\nFor more information, see https://docs.github.com/en/migrations/using-github-enterprise-importer/preparing-to-migrate-with-github-enterprise-importer/managing-access-for-github-enterprise-importer.", org)
}

func areBlobCredentialsRequired(ctx context.Context, vf migrateRepoVersionFetcher, ghesAPIURL string, log *logger.Logger) (bool, error) {
	if strings.TrimSpace(ghesAPIURL) == "" {
		return false, nil
	}

	log.Info("Using GitHub Enterprise Server - verifying server version")

	vi, err := vf.GetVersion(ctx)
	if err != nil {
		return false, fmt.Errorf("getting GHES version: %w", err)
	}

	version := vi.Version
	if version == "" {
		return true, nil
	}

	log.Info("GitHub Enterprise Server version %s detected", version)

	parts := strings.Split(strings.TrimSpace(version), ".")
	if len(parts) != 3 {
		log.Info("Unable to parse the version number, defaulting to using CLI for blob storage uploads")
		return true, nil
	}

	// Parse major.minor.patch
	var nums [3]int
	for i, p := range parts {
		n := 0
		for _, c := range p {
			if c < '0' || c > '9' {
				log.Info("Unable to parse the version number, defaulting to using CLI for blob storage uploads")
				return true, nil
			}
			n = n*10 + int(c-'0')
		}
		nums[i] = n
	}

	// Versions < 3.8.0 require external storage
	threshold := [3]int{3, 8, 0}
	if nums[0] < threshold[0] ||
		(nums[0] == threshold[0] && nums[1] < threshold[1]) ||
		(nums[0] == threshold[0] && nums[1] == threshold[1] && nums[2] < threshold[2]) {
		return true, nil
	}

	return false, nil
}

func validateUploadOptions(a *migrateRepoArgs, envProv migrateRepoEnvProvider, cloudCredentialsRequired bool, log *logger.Logger) error {
	shouldUseAzure := resolveAzureConnectionString(a.azureStorageConnectionString, envProv) != ""
	shouldUseAWS := a.awsBucketName != ""

	if !cloudCredentialsRequired {
		if shouldUseAzure {
			log.Warning("Ignoring provided Azure Blob Storage credentials because you are running GitHub Enterprise Server (GHES) 3.8.0 or later.")
		}
		if shouldUseAWS {
			log.Warning("Ignoring provided AWS S3 credentials because you are running GitHub Enterprise Server (GHES) 3.8.0 or later.")
		}
		if a.useGithubStorage {
			log.Warning("Providing the --use-github-storage flag will supersede any credentials you have configured in your GitHub Enterprise Server (GHES) Management Console.")
		}
		if a.keepArchive {
			log.Warning("Ignoring --keep-archive option because there is no downloaded archive to keep")
		}
		return nil
	}

	if !shouldUseAzure && !shouldUseAWS && !a.useGithubStorage {
		return cmdutil.NewUserError(
			"Either Azure storage connection (--azure-storage-connection-string or AZURE_STORAGE_CONNECTION_STRING env. variable) or " +
				"AWS S3 connection (--aws-bucket-name, --aws-access-key (or AWS_ACCESS_KEY_ID env. variable), --aws-secret-key (or AWS_SECRET_ACCESS_KEY env.variable)) or " +
				"GitHub Storage Option (--use-github-storage) " +
				"must be provided.")
	}

	if shouldUseAzure && shouldUseAWS {
		return cmdutil.NewUserError(
			"Azure storage connection and AWS S3 connection cannot be specified together.")
	}

	if shouldUseAWS {
		if resolveAWSAccessKey(a.awsAccessKey, envProv) == "" {
			return cmdutil.NewUserError("Either --aws-access-key or AWS_ACCESS_KEY_ID environment variable must be set.")
		}
		if resolveAWSSecretKey(a.awsSecretKey, envProv) == "" {
			return cmdutil.NewUserError("Either --aws-secret-key or AWS_SECRET_ACCESS_KEY environment variable must be set.")
		}
		if resolveAWSRegion(a.awsRegion, envProv) == "" {
			return cmdutil.NewUserError("Either --aws-region or AWS_REGION environment variable must be set.")
		}
	} else if a.awsAccessKey != "" || a.awsSecretKey != "" || a.awsSessionToken != "" || a.awsRegion != "" {
		return cmdutil.NewUserError("The AWS S3 bucket name must be provided with --aws-bucket-name if other AWS S3 upload options are set.")
	}

	return nil
}

func generateAndUploadArchives(
	ctx context.Context,
	sourceGH migrateRepoSourceGitHub,
	targetGH migrateRepoTargetGitHub,
	uploader migrateRepoArchiveUploader,
	downloader migrateRepoHTTPDownloader,
	fs migrateRepoFileSystem,
	log *logger.Logger,
	a *migrateRepoArgs,
	blobCredentialsRequired bool,
	opts migrateRepoOptions,
) (string, string, error) {
	// Start archive generation
	gitArchiveID, err := sourceGH.StartGitArchiveGeneration(ctx, a.githubSourceOrg, a.sourceRepo)
	if err != nil {
		return "", "", err
	}
	log.Info("Archive generation of git data started with id: %d", gitArchiveID)

	metadataArchiveID, err := sourceGH.StartMetadataArchiveGeneration(ctx, a.githubSourceOrg, a.sourceRepo, a.skipReleases, a.lockSourceRepo)
	if err != nil {
		return "", "", err
	}
	log.Info("Archive generation of metadata started with id: %d", metadataArchiveID)

	// Wait for archives
	archiveTimeout := opts.archiveTimeout
	if archiveTimeout == 0 {
		archiveTimeout = archiveGenerationTimeoutDefault
	}
	archivePollInterval := opts.archivePollInterval

	gitArchiveURL, err := waitForArchiveGeneration(ctx, sourceGH, a.githubSourceOrg, gitArchiveID, archiveTimeout, archivePollInterval, log)
	if err != nil {
		return "", "", err
	}
	log.Info("Archive (git) download url: %s", gitArchiveURL)

	metadataArchiveURL, err := waitForArchiveGeneration(ctx, sourceGH, a.githubSourceOrg, metadataArchiveID, archiveTimeout, archivePollInterval, log)
	if err != nil {
		return "", "", err
	}
	log.Info("Archive (metadata) download url: %s", metadataArchiveURL)

	// If no blob upload needed, return the URLs directly
	if !a.useGithubStorage && !blobCredentialsRequired {
		return gitArchiveURL, metadataArchiveURL, nil
	}

	// Download and upload
	timeNow := time.Now().Format("2006-01-02_15-04-05")
	gitUploadName := fmt.Sprintf("%s-%d-%s", timeNow, gitArchiveID, gitArchiveFileName)
	metaUploadName := fmt.Sprintf("%s-%d-%s", timeNow, metadataArchiveID, metadataArchiveFileName)

	gitTempFile := fs.GetTempFileName()
	metaTempFile := fs.GetTempFileName()

	defer func() {
		if !a.keepArchive {
			if err := fs.DeleteIfExists(gitTempFile); err != nil {
				log.Warning("Couldn't delete the downloaded archive at %q: %s", gitTempFile, err)
			}
			if err := fs.DeleteIfExists(metaTempFile); err != nil {
				log.Warning("Couldn't delete the downloaded archive at %q: %s", metaTempFile, err)
			}
		}
	}()

	// Download git archive
	log.Info("Downloading archive from %s", gitArchiveURL)
	if err := downloader.DownloadToFile(ctx, gitArchiveURL, gitTempFile); err != nil {
		return "", "", err
	}
	if a.keepArchive {
		log.Info("Git archive was successfully downloaded at %q", gitTempFile)
	} else {
		log.Info("Download complete")
	}

	// Download metadata archive
	log.Info("Downloading archive from %s", metadataArchiveURL)
	if err := downloader.DownloadToFile(ctx, metadataArchiveURL, metaTempFile); err != nil {
		return "", "", err
	}
	if a.keepArchive {
		log.Info("Metadata archive was successfully downloaded at %q", metaTempFile)
	} else {
		log.Info("Download complete")
	}

	// Upload git archive
	gitBlobURL, err := uploadArchiveFile(ctx, uploader, fs, a.githubTargetOrg, gitTempFile, gitUploadName)
	if err != nil {
		return "", "", err
	}

	// Upload metadata archive
	metaBlobURL, err := uploadArchiveFile(ctx, uploader, fs, a.githubTargetOrg, metaTempFile, metaUploadName)
	if err != nil {
		return "", "", err
	}

	return gitBlobURL, metaBlobURL, nil
}

func uploadLocalArchives(
	ctx context.Context,
	uploader migrateRepoArchiveUploader,
	fs migrateRepoFileSystem,
	log *logger.Logger,
	a *migrateRepoArgs,
) (string, string, error) {
	timeNow := time.Now().Format("2006-01-02_15-04-05")
	sameArchive := a.gitArchivePath == a.metadataArchivePath

	var gitUploadName, metaUploadName string
	if sameArchive {
		gitUploadName = fmt.Sprintf("%s-%s", timeNow, duplicateArchiveFileName)
		metaUploadName = gitUploadName
	} else {
		gitUploadName = fmt.Sprintf("%s-%s", timeNow, gitArchiveFileName)
		metaUploadName = fmt.Sprintf("%s-%s", timeNow, metadataArchiveFileName)
	}

	gitBlobURL, err := uploadArchiveFile(ctx, uploader, fs, a.githubTargetOrg, a.gitArchivePath, gitUploadName)
	if err != nil {
		return "", "", err
	}

	metaBlobURL := gitBlobURL
	if !sameArchive {
		metaBlobURL, err = uploadArchiveFile(ctx, uploader, fs, a.githubTargetOrg, a.metadataArchivePath, metaUploadName)
		if err != nil {
			return "", "", err
		}
	}

	return gitBlobURL, metaBlobURL, nil
}

func uploadArchiveFile(ctx context.Context, uploader migrateRepoArchiveUploader, fs migrateRepoFileSystem, targetOrg, filePath, uploadName string) (string, error) {
	content, size, err := fs.OpenRead(filePath)
	if err != nil {
		return "", fmt.Errorf("opening archive %s: %w", filePath, err)
	}
	defer content.Close()

	return uploader.Upload(ctx, targetOrg, uploadName, content, size)
}

func waitForArchiveGeneration(
	ctx context.Context,
	gh migrateRepoSourceGitHub,
	org string,
	archiveID int,
	timeout, pollInterval time.Duration,
	log *logger.Logger,
) (string, error) {
	deadline := time.Now().Add(timeout)

	for time.Now().Before(deadline) {
		status, err := gh.GetArchiveMigrationStatus(ctx, org, archiveID)
		if err != nil {
			return "", err
		}

		log.Info("Waiting for archive with id %d generation to finish. Current status: %s", archiveID, status)

		if strings.EqualFold(status, "exported") {
			archiveURL, err := gh.GetArchiveMigrationUrl(ctx, org, archiveID)
			if err != nil {
				return "", err
			}
			return archiveURL, nil
		}

		if strings.EqualFold(status, "failed") {
			return "", cmdutil.NewUserErrorf("Archive generation failed for id: %d", archiveID)
		}

		select {
		case <-ctx.Done():
			return "", ctx.Err()
		case <-time.After(pollInterval):
		}
	}

	return "", fmt.Errorf("archive generation timed out after %s", timeout)
}

func buildSourceRepoURL(org, repo, ghesAPIURL string) string {
	baseURL := defaultGitHubBaseURL
	if strings.TrimSpace(ghesAPIURL) != "" {
		baseURL = extractGHESBaseURL(ghesAPIURL)
	}
	return fmt.Sprintf("%s/%s/%s", baseURL, url.PathEscape(org), url.PathEscape(repo))
}

var (
	ghesAPIV3Pattern = regexp.MustCompile(`(?i)(?P<baseUrl>https?://.+)/api/v3`)
	ghesAPIDomain    = regexp.MustCompile(`(?i)(?P<scheme>https?):\/\/api\.(?P<host>.+)`)
)

func extractGHESBaseURL(ghesAPIURL string) string {
	ghesAPIURL = strings.TrimSpace(ghesAPIURL)
	ghesAPIURL = strings.TrimRight(ghesAPIURL, "/")

	if m := ghesAPIV3Pattern.FindStringSubmatch(ghesAPIURL); len(m) > 1 {
		return m[1]
	}

	if m := ghesAPIDomain.FindStringSubmatch(ghesAPIURL); len(m) > 2 {
		return fmt.Sprintf("%s://%s", m[1], m[2])
	}

	return ghesAPIURL
}

func resolveSourceToken(flagValue string, envProv migrateRepoEnvProvider) string {
	if flagValue != "" {
		return flagValue
	}
	if v := envProv.SourceGitHubPAT(); v != "" {
		return v
	}
	return envProv.TargetGitHubPAT()
}

func resolveTargetToken(flagValue string, envProv migrateRepoEnvProvider) string {
	if flagValue != "" {
		return flagValue
	}
	return envProv.TargetGitHubPAT()
}

func resolveAzureConnectionString(flagValue string, envProv migrateRepoEnvProvider) string {
	if flagValue != "" {
		return flagValue
	}
	return envProv.AzureStorageConnectionString()
}

func resolveAWSAccessKey(flagValue string, envProv migrateRepoEnvProvider) string {
	if flagValue != "" {
		return flagValue
	}
	return envProv.AWSAccessKeyID()
}

func resolveAWSSecretKey(flagValue string, envProv migrateRepoEnvProvider) string {
	if flagValue != "" {
		return flagValue
	}
	return envProv.AWSSecretAccessKey()
}

func resolveAWSRegion(flagValue string, envProv migrateRepoEnvProvider) string {
	if flagValue != "" {
		return flagValue
	}
	return envProv.AWSRegion()
}

// ---------------------------------------------------------------------------
// Production filesystem adapter
// ---------------------------------------------------------------------------

// fsAdapter wraps filesystem.Provider to satisfy migrateRepoFileSystem.
type fsAdapter struct {
	prov *filesystem.Provider
}

func (a *fsAdapter) GetTempFileName() string {
	name, err := a.prov.GetTempFileName()
	if err != nil {
		// Fallback: caller will get an empty path and subsequent file I/O will fail
		// with a clear error.
		return ""
	}
	return name
}

func (a *fsAdapter) OpenRead(path string) (io.ReadSeekCloser, int64, error) {
	f, err := os.Open(path)
	if err != nil {
		return nil, 0, err
	}
	info, err := f.Stat()
	if err != nil {
		f.Close()
		return nil, 0, err
	}
	return f, info.Size(), nil
}

func (a *fsAdapter) DeleteIfExists(path string) error {
	if !a.prov.FileExists(path) {
		return nil
	}
	return a.prov.DeleteFile(path)
}

func (a *fsAdapter) FileExists(path string) bool {
	return a.prov.FileExists(path)
}

// envProviderAdapter wraps env.Provider to satisfy migrateRepoEnvProvider.
type envProviderAdapter struct {
	prov *env.Provider
}

func (a *envProviderAdapter) SourceGitHubPAT() string { return a.prov.SourceGitHubPAT() }
func (a *envProviderAdapter) TargetGitHubPAT() string { return a.prov.TargetGitHubPAT() }
func (a *envProviderAdapter) AzureStorageConnectionString() string {
	return a.prov.AzureStorageConnectionString()
}
func (a *envProviderAdapter) AWSAccessKeyID() string     { return a.prov.AWSAccessKeyID() }
func (a *envProviderAdapter) AWSSecretAccessKey() string { return a.prov.AWSSecretAccessKey() }
func (a *envProviderAdapter) AWSSessionToken() string    { return a.prov.AWSSessionToken() }
func (a *envProviderAdapter) AWSRegion() string          { return a.prov.AWSRegion() }

// ---------------------------------------------------------------------------
// Production command constructor (used by main.go)
// ---------------------------------------------------------------------------

// newMigrateRepoCmdLive creates the migrate-repo command with real deps
// constructed at runtime from resolved flags/env.
func newMigrateRepoCmdLive() *cobra.Command {
	var (
		githubSourceOrg              string
		sourceRepo                   string
		githubTargetOrg              string
		targetRepo                   string
		targetAPIURL                 string
		targetUploadsURL             string
		ghesAPIURL                   string
		azureStorageConnectionString string
		awsBucketName                string
		awsAccessKey                 string
		awsSecretKey                 string
		awsSessionToken              string
		awsRegion                    string
		noSSLVerify                  bool
		gitArchiveURL                string
		metadataArchiveURL           string
		gitArchivePath               string
		metadataArchivePath          string
		skipReleases                 bool
		lockSourceRepo               bool
		queueOnly                    bool
		targetRepoVisibility         string
		githubSourcePAT              string
		githubTargetPAT              string
		keepArchive                  bool
		useGithubStorage             bool
	)

	cmd := &cobra.Command{
		Use:   "migrate-repo",
		Short: "Migrates a repository to GitHub using GitHub Enterprise Importer",
		Long:  "Migrates a repository from GitHub or GitHub Enterprise Server to GitHub.com using GitHub Enterprise Importer.",
		RunE: func(cmd *cobra.Command, _ []string) error {
			log := getLogger(cmd)
			ctx := cmd.Context()
			envProv := &envProviderAdapter{prov: env.New()}
			fsProvider := &fsAdapter{prov: filesystem.New()}

			a := migrateRepoArgs{
				githubSourceOrg:              githubSourceOrg,
				sourceRepo:                   sourceRepo,
				githubTargetOrg:              githubTargetOrg,
				targetRepo:                   targetRepo,
				targetAPIURL:                 targetAPIURL,
				targetUploadsURL:             targetUploadsURL,
				ghesAPIURL:                   ghesAPIURL,
				azureStorageConnectionString: azureStorageConnectionString,
				awsBucketName:                awsBucketName,
				awsAccessKey:                 awsAccessKey,
				awsSecretKey:                 awsSecretKey,
				awsSessionToken:              awsSessionToken,
				awsRegion:                    awsRegion,
				noSSLVerify:                  noSSLVerify,
				gitArchiveURL:                gitArchiveURL,
				metadataArchiveURL:           metadataArchiveURL,
				gitArchivePath:               gitArchivePath,
				metadataArchivePath:          metadataArchivePath,
				skipReleases:                 skipReleases,
				lockSourceRepo:               lockSourceRepo,
				queueOnly:                    queueOnly,
				targetRepoVisibility:         targetRepoVisibility,
				githubSourcePAT:              githubSourcePAT,
				githubTargetPAT:              githubTargetPAT,
				keepArchive:                  keepArchive,
				useGithubStorage:             useGithubStorage,
			}

			// Validate first (populates defaults and resolves env vars)
			if err := validateMigrateRepoArgs(&a, envProv, log); err != nil {
				return err
			}

			// Now construct GitHub clients with resolved tokens
			sourceToken := resolveSourceToken(a.githubSourcePAT, envProv)
			targetToken := resolveTargetToken(a.githubTargetPAT, envProv)

			sourceAPIURL := a.ghesAPIURL
			if sourceAPIURL == "" {
				sourceAPIURL = defaultGitHubAPIURL
			}

			sourceOpts := []github.Option{
				github.WithAPIURL(sourceAPIURL),
				github.WithLogger(log),
				github.WithVersion(version),
			}
			if a.noSSLVerify {
				sourceOpts = append(sourceOpts, github.WithNoSSLVerify())
			}
			sourceGH := github.NewClient(sourceToken, sourceOpts...)

			tgtAPI := a.targetAPIURL
			if tgtAPI == "" {
				tgtAPI = defaultGitHubAPIURL
			}
			targetGH := github.NewClient(targetToken,
				github.WithAPIURL(tgtAPI),
				github.WithLogger(log),
				github.WithVersion(version),
			)

			// Build archive uploader with resolved credentials
			uploaderOpts := []archive.UploaderOption{archive.WithLogger(log)}
			// NOTE: Azure and AWS storage backends require their respective client
			// packages. When those packages are fully integrated, add:
			//   if connStr != "" { uploaderOpts = append(uploaderOpts, archive.WithAzure(...)) }
			//   if awsBucketName != "" { uploaderOpts = append(uploaderOpts, archive.WithAWS(...)) }
			if a.useGithubStorage {
				// TODO: GitHub-owned storage requires Upload method on github.Client
				// which has not been implemented yet. This is a hidden flag.
				log.Warning("GitHub-owned storage is not yet fully implemented in the Go port")
			}
			uploader := archive.NewUploader(uploaderOpts...)

			downloader := download.New(nil) // default HTTP client

			opts := migrateRepoOptions{
				pollInterval:        migrationPollIntervalDefault,
				archivePollInterval: archivePollIntervalDefault,
				archiveTimeout:      archiveGenerationTimeoutDefault,
			}

			return runMigrateRepo(ctx, a, sourceGH, targetGH, envProv, uploader, downloader, fsProvider, log, opts)
		},
	}

	// Required flags
	cmd.Flags().StringVar(&githubSourceOrg, "github-source-org", "", "Source GitHub organization (REQUIRED)")
	cmd.Flags().StringVar(&sourceRepo, "source-repo", "", "Source repository name (REQUIRED)")
	cmd.Flags().StringVar(&githubTargetOrg, "github-target-org", "", "Target GitHub organization (REQUIRED)")

	// Optional flags
	cmd.Flags().StringVar(&targetRepo, "target-repo", "", "Target repository name (defaults to source-repo)")
	cmd.Flags().StringVar(&targetAPIURL, "target-api-url", "", "API URL for the target GitHub instance (defaults to https://api.github.com)")
	cmd.Flags().StringVar(&targetUploadsURL, "target-uploads-url", "", "Uploads URL for the target GitHub instance")
	cmd.Flags().StringVar(&ghesAPIURL, "ghes-api-url", "", "API endpoint for GHES instance")
	cmd.Flags().StringVar(&azureStorageConnectionString, "azure-storage-connection-string", "", "Azure Blob Storage connection string")
	cmd.Flags().StringVar(&awsBucketName, "aws-bucket-name", "", "AWS S3 bucket name")
	cmd.Flags().StringVar(&awsAccessKey, "aws-access-key", "", "AWS access key (falls back to AWS_ACCESS_KEY_ID env)")
	cmd.Flags().StringVar(&awsSecretKey, "aws-secret-key", "", "AWS secret key (falls back to AWS_SECRET_ACCESS_KEY env)")
	cmd.Flags().StringVar(&awsSessionToken, "aws-session-token", "", "AWS session token (falls back to AWS_SESSION_TOKEN env)")
	cmd.Flags().StringVar(&awsRegion, "aws-region", "", "AWS region (falls back to AWS_REGION env)")
	cmd.Flags().BoolVar(&noSSLVerify, "no-ssl-verify", false, "Disable SSL verification for GHES")
	cmd.Flags().StringVar(&gitArchiveURL, "git-archive-url", "", "URL for pre-generated git archive")
	cmd.Flags().StringVar(&metadataArchiveURL, "metadata-archive-url", "", "URL for pre-generated metadata archive")
	cmd.Flags().StringVar(&gitArchivePath, "git-archive-path", "", "Path to local git archive file")
	cmd.Flags().StringVar(&metadataArchivePath, "metadata-archive-path", "", "Path to local metadata archive file")
	cmd.Flags().BoolVar(&skipReleases, "skip-releases", false, "Skip releases when migrating")
	cmd.Flags().BoolVar(&lockSourceRepo, "lock-source-repo", false, "Lock source repository during migration")
	cmd.Flags().BoolVar(&queueOnly, "queue-only", false, "Queue the migration without waiting for completion")
	cmd.Flags().StringVar(&targetRepoVisibility, "target-repo-visibility", "", "Target repository visibility (public, private, internal)")
	cmd.Flags().StringVar(&githubSourcePAT, "github-source-pat", "", "Personal access token for the source GitHub instance")
	cmd.Flags().StringVar(&githubTargetPAT, "github-target-pat", "", "Personal access token for the target GitHub instance")
	cmd.Flags().BoolVar(&keepArchive, "keep-archive", false, "Keep downloaded archive files after upload")
	cmd.Flags().BoolVar(&useGithubStorage, "use-github-storage", false, "Use GitHub-owned storage for archives")

	// Hidden flags
	_ = cmd.Flags().MarkHidden("git-archive-url")
	_ = cmd.Flags().MarkHidden("metadata-archive-url")
	_ = cmd.Flags().MarkHidden("git-archive-path")
	_ = cmd.Flags().MarkHidden("metadata-archive-path")
	_ = cmd.Flags().MarkHidden("target-uploads-url")
	_ = cmd.Flags().MarkHidden("use-github-storage")

	return cmd
}
