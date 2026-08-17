package main

import (
	"context"
	"fmt"
	"io"
	"net/http"
	"net/url"
	"strconv"
	"strings"
	"time"

	"github.com/github/gh-gei/internal/cmdutil"
	"github.com/github/gh-gei/internal/sharedcmd"
	"github.com/github/gh-gei/pkg/archive"
	"github.com/github/gh-gei/pkg/bbs"
	"github.com/github/gh-gei/pkg/env"
	"github.com/github/gh-gei/pkg/filesystem"
	"github.com/github/gh-gei/pkg/github"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/github/gh-gei/pkg/migration"
	awsStorage "github.com/github/gh-gei/pkg/storage/aws"
	azureStorage "github.com/github/gh-gei/pkg/storage/azure"
	"github.com/github/gh-gei/pkg/storage/ghowned"
	"github.com/google/uuid"
	"github.com/spf13/cobra"
)

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

const (
	bbsMigrationPollInterval = 60 * time.Second
	bbsExportPollInterval    = 10 * time.Second
	bbsDefaultTargetAPIURL   = "https://api.github.com"
	bbsDefaultSSHPort        = 22
)

// ---------------------------------------------------------------------------
// Consumer-defined interfaces
// ---------------------------------------------------------------------------

// bbsMigrateRepoGitHub defines methods needed from the GitHub API.
type bbsMigrateRepoGitHub interface {
	DoesRepoExist(ctx context.Context, org, repo string) (bool, error)
	GetOrganizationId(ctx context.Context, org string) (string, error)
	CreateBbsMigrationSource(ctx context.Context, orgID string) (string, error)
	StartBbsMigration(ctx context.Context, migrationSourceID, bbsRepoURL, orgID, repo, targetToken, archiveURL, targetRepoVisibility string) (string, error)
	GetMigration(ctx context.Context, id string) (*github.Migration, error)
}

// bbsMigrateRepoBbsAPI defines methods needed from the Bitbucket Server API.
type bbsMigrateRepoBbsAPI interface {
	StartExport(ctx context.Context, projectKey, slug string) (int64, error)
	GetExport(ctx context.Context, id int64) (state string, message string, percentage int, err error)
}

// bbsMigrateRepoArchiveDownloader downloads an export archive from BBS.
type bbsMigrateRepoArchiveDownloader interface {
	Download(exportJobID int64, targetDirectory string) (string, error)
}

// bbsMigrateRepoArchiveUploader uploads an archive to blob storage.
type bbsMigrateRepoArchiveUploader interface {
	Upload(ctx context.Context, targetOrg, fileName string, content io.ReadSeeker, size int64) (string, error)
}

// bbsMigrateRepoFileSystem provides filesystem operations.
type bbsMigrateRepoFileSystem interface {
	FileExists(path string) bool
	DirectoryExists(path string) bool
	OpenRead(path string) (io.ReadSeekCloser, int64, error)
	DeleteIfExists(path string) error
}

// bbsMigrateRepoEnvProvider provides environment variable fallbacks.
type bbsMigrateRepoEnvProvider interface {
	TargetGitHubPAT() string
	AzureStorageConnectionString() string
	AWSAccessKeyID() string
	AWSSecretAccessKey() string
	AWSSessionToken() string
	AWSRegion() string
	BBSUsername() string
	BBSPassword() string
	SmbPassword() string
}

// ---------------------------------------------------------------------------
// Options (configurable for testing)
// ---------------------------------------------------------------------------

type bbsMigrateRepoOptions struct {
	pollInterval       time.Duration
	exportPollInterval time.Duration
}

// ---------------------------------------------------------------------------
// Args struct
// ---------------------------------------------------------------------------

type bbsMigrateRepoArgs struct {
	bbsServerURL                 string
	bbsProject                   string
	bbsRepo                      string
	bbsUsername                  string
	bbsPassword                  string
	noSSLVerify                  bool
	kerberos                     bool
	sshUser                      string
	sshPrivateKey                string
	sshPort                      int
	smbUser                      string
	smbPassword                  string
	smbDomain                    string
	archivePath                  string
	archiveURL                   string
	archiveDownloadHost          string
	bbsSharedHome                string
	githubOrg                    string
	githubRepo                   string
	githubPAT                    string
	targetRepoVisibility         string
	azureStorageConnectionString string
	awsBucketName                string
	awsAccessKey                 string
	awsSecretKey                 string
	awsSessionToken              string
	awsRegion                    string
	keepArchive                  bool
	targetAPIURL                 string
	targetUploadsURL             string
	queueOnly                    bool
	useGithubStorage             bool
}

// Phase predicates — ported from C# MigrateRepoCommandArgs.
func (a *bbsMigrateRepoArgs) shouldGenerateArchive() bool {
	return a.bbsServerURL != "" && a.archivePath == "" && a.archiveURL == ""
}

func (a *bbsMigrateRepoArgs) shouldDownloadArchive() bool {
	return a.sshUser != "" || a.smbUser != ""
}

func (a *bbsMigrateRepoArgs) shouldUploadArchive() bool {
	return a.archiveURL == "" && a.githubOrg != ""
}

func (a *bbsMigrateRepoArgs) shouldImportArchive() bool {
	return a.archiveURL != "" || a.githubOrg != ""
}

// ---------------------------------------------------------------------------
// Command constructor (testable — accepts interfaces)
// ---------------------------------------------------------------------------

func newBbsMigrateRepoCmd(
	gh bbsMigrateRepoGitHub,
	bbsAPI bbsMigrateRepoBbsAPI,
	downloader bbsMigrateRepoArchiveDownloader,
	uploader bbsMigrateRepoArchiveUploader,
	fs bbsMigrateRepoFileSystem,
	envProv bbsMigrateRepoEnvProvider,
	log *logger.Logger,
	opts bbsMigrateRepoOptions,
) *cobra.Command {
	var a bbsMigrateRepoArgs

	cmd := &cobra.Command{
		Use:   "migrate-repo",
		Short: "Migrate a Bitbucket Server repository to GitHub",
		Long:  "Exports a Bitbucket Server repository, uploads it to blob storage, and imports it into GitHub using GitHub Enterprise Importer.",
		RunE: func(cmd *cobra.Command, _ []string) error {
			return runBbsMigrateRepo(cmd.Context(), &a, gh, bbsAPI, downloader, uploader, fs, envProv, log, opts)
		},
	}

	// BBS flags
	cmd.Flags().StringVar(&a.bbsServerURL, "bbs-server-url", "", "The full URL of the Bitbucket Server/Data Center instance")
	cmd.Flags().StringVar(&a.bbsProject, "bbs-project", "", "The Bitbucket Server project key")
	cmd.Flags().StringVar(&a.bbsRepo, "bbs-repo", "", "The Bitbucket Server repository slug")
	cmd.Flags().StringVar(&a.bbsUsername, "bbs-username", "", "Bitbucket Server username (falls back to BBS_USERNAME env)")
	cmd.Flags().StringVar(&a.bbsPassword, "bbs-password", "", "Bitbucket Server password (falls back to BBS_PASSWORD env)")
	cmd.Flags().BoolVar(&a.noSSLVerify, "no-ssl-verify", false, "Disable SSL verification for BBS")
	cmd.Flags().BoolVar(&a.kerberos, "kerberos", false, "Use Kerberos authentication for BBS")

	// SSH download flags
	cmd.Flags().StringVar(&a.sshUser, "ssh-user", "", "SSH username for downloading export archive")
	cmd.Flags().StringVar(&a.sshPrivateKey, "ssh-private-key", "", "Path to SSH private key for downloading export archive")
	cmd.Flags().IntVar(&a.sshPort, "ssh-port", bbsDefaultSSHPort, "SSH port for downloading export archive")

	// SMB download flags
	cmd.Flags().StringVar(&a.smbUser, "smb-user", "", "SMB username for downloading export archive")
	cmd.Flags().StringVar(&a.smbPassword, "smb-password", "", "SMB password (falls back to SMB_PASSWORD env)")
	cmd.Flags().StringVar(&a.smbDomain, "smb-domain", "", "SMB domain for authentication")

	// Archive flags
	cmd.Flags().StringVar(&a.archivePath, "archive-path", "", "Path to a local BBS export archive file")
	cmd.Flags().StringVar(&a.archiveURL, "archive-url", "", "URL to a pre-uploaded BBS export archive")
	cmd.Flags().StringVar(&a.archiveDownloadHost, "archive-download-host", "", "Override host for downloading export archive via SSH/SMB")
	cmd.Flags().StringVar(&a.bbsSharedHome, "bbs-shared-home", bbs.DefaultBbsSharedHomeDirectoryLinux, "Path to Bitbucket Server shared home directory")

	// GitHub target flags
	cmd.Flags().StringVar(&a.githubOrg, "github-org", "", "Target GitHub organization")
	cmd.Flags().StringVar(&a.githubRepo, "github-repo", "", "Target GitHub repository name")
	cmd.Flags().StringVar(&a.githubPAT, "github-pat", "", "Personal access token for the target GitHub instance")
	cmd.Flags().StringVar(&a.targetRepoVisibility, "target-repo-visibility", "", "Target repository visibility (public, private, internal)")
	cmd.Flags().StringVar(&a.targetAPIURL, "target-api-url", bbsDefaultTargetAPIURL, "API URL for the target GitHub instance")

	// Upload storage flags
	cmd.Flags().StringVar(&a.azureStorageConnectionString, "azure-storage-connection-string", "", "Azure Blob Storage connection string")
	cmd.Flags().StringVar(&a.awsBucketName, "aws-bucket-name", "", "AWS S3 bucket name")
	cmd.Flags().StringVar(&a.awsAccessKey, "aws-access-key", "", "AWS access key (falls back to AWS_ACCESS_KEY_ID env)")
	cmd.Flags().StringVar(&a.awsSecretKey, "aws-secret-key", "", "AWS secret key (falls back to AWS_SECRET_ACCESS_KEY env)")
	cmd.Flags().StringVar(&a.awsSessionToken, "aws-session-token", "", "AWS session token (falls back to AWS_SESSION_TOKEN env)")
	cmd.Flags().StringVar(&a.awsRegion, "aws-region", "", "AWS region (falls back to AWS_REGION env)")

	// Behavior flags
	cmd.Flags().BoolVar(&a.keepArchive, "keep-archive", false, "Keep downloaded archive files after upload")
	cmd.Flags().BoolVar(&a.queueOnly, "queue-only", false, "Queue the migration without waiting for completion")
	cmd.Flags().BoolVar(&a.useGithubStorage, "use-github-storage", false, "Use GitHub-owned storage for archives")

	return cmd
}

// ---------------------------------------------------------------------------
// Validation
// ---------------------------------------------------------------------------

func validateBbsMigrateRepoArgs(a *bbsMigrateRepoArgs, envProv bbsMigrateRepoEnvProvider, fs bbsMigrateRepoFileSystem, log *logger.Logger) error {
	if err := validateBbsSourceArgs(a); err != nil {
		return err
	}

	if err := cmdutil.ValidateOneOf(a.targetRepoVisibility, "--target-repo-visibility", "public", "private", "internal"); err != nil {
		return err
	}

	if a.shouldGenerateArchive() {
		if err := validateBbsArchiveGeneration(a, envProv, fs, log); err != nil {
			return err
		}
	} else {
		if err := validateBbsPrebuiltArchive(a, fs); err != nil {
			return err
		}
	}

	if a.shouldUploadArchive() {
		if err := validateBbsUploadOptions(a, envProv); err != nil {
			return err
		}
	}

	if a.sshPort == 7999 {
		log.Warning("--ssh-port is set to 7999, which is the default port that Bitbucket Server and Bitbucket Data Center use for Git operations over SSH. This is probably the wrong value, because --ssh-port should be configured with the SSH port used to manage the server where Bitbucket Server/Bitbucket Data Center is running, not the port used for Git operations over SSH.")
	}

	return nil
}

func validateBbsSourceArgs(a *bbsMigrateRepoArgs) error {
	if a.bbsServerURL == "" && a.archiveURL == "" && a.archivePath == "" {
		return cmdutil.NewUserError("Either --bbs-server-url, --archive-path, or --archive-url must be specified.")
	}
	if a.archivePath != "" && a.archiveURL != "" {
		return cmdutil.NewUserError("Only one of --archive-path or --archive-url can be specified.")
	}
	if a.shouldImportArchive() {
		if err := cmdutil.ValidateRequired(a.githubOrg, "--github-org"); err != nil {
			return err
		}
		if err := cmdutil.ValidateRequired(a.githubRepo, "--github-repo"); err != nil {
			return err
		}
	}
	return nil
}

func validateBbsArchiveGeneration(a *bbsMigrateRepoArgs, envProv bbsMigrateRepoEnvProvider, fs bbsMigrateRepoFileSystem, log *logger.Logger) error {
	if err := validateBbsGenerateOptions(a, envProv); err != nil {
		return err
	}
	if err := validateBbsDownloadOptions(a, envProv, log); err != nil {
		return err
	}

	smbPassword := a.smbPassword
	if smbPassword == "" {
		smbPassword = envProv.SmbPassword()
	}
	if (a.smbUser != "" && smbPassword == "") || (a.smbPassword != "" && a.smbUser == "") {
		return cmdutil.NewUserError("Both --smb-user and --smb-password (or SMB_PASSWORD env. variable) must be specified for SMB download.")
	}

	if !a.shouldDownloadArchive() {
		if !fs.DirectoryExists(a.bbsSharedHome) {
			return cmdutil.NewUserErrorf("The BBS shared home directory '%s' does not exist.", a.bbsSharedHome)
		}
	}
	return nil
}

func validateBbsPrebuiltArchive(a *bbsMigrateRepoArgs, fs bbsMigrateRepoFileSystem) error {
	if err := validateNoGenerateOptions(a); err != nil {
		return err
	}
	if a.archivePath != "" && !fs.FileExists(a.archivePath) {
		return cmdutil.NewUserErrorf("The archive file '%s' does not exist.", a.archivePath)
	}
	return nil
}

func validateBbsGenerateOptions(a *bbsMigrateRepoArgs, envProv bbsMigrateRepoEnvProvider) error {
	if err := cmdutil.ValidateRequired(a.bbsProject, "--bbs-project"); err != nil {
		return err
	}
	if err := cmdutil.ValidateRequired(a.bbsRepo, "--bbs-repo"); err != nil {
		return err
	}

	// Kerberos conflicts with username/password
	if a.kerberos {
		if a.bbsUsername != "" || a.bbsPassword != "" {
			return cmdutil.NewUserError("--bbs-username and --bbs-password cannot be used with --kerberos")
		}
		return nil
	}

	// Resolve BBS credentials from flags or env
	username := a.bbsUsername
	if username == "" {
		username = envProv.BBSUsername()
	}
	if username == "" {
		return cmdutil.NewUserError("BBS username must be provided via --bbs-username or BBS_USERNAME environment variable")
	}

	password := a.bbsPassword
	if password == "" {
		password = envProv.BBSPassword()
	}
	if password == "" {
		return cmdutil.NewUserError("BBS password must be provided via --bbs-password or BBS_PASSWORD environment variable")
	}

	return nil
}

func validateBbsDownloadOptions(a *bbsMigrateRepoArgs, _ bbsMigrateRepoEnvProvider, log *logger.Logger) error {
	// SSH and SMB are mutually exclusive
	if a.sshUser != "" && a.smbUser != "" {
		return cmdutil.NewUserError("--ssh-user and --smb-user cannot be used together")
	}

	// archive-download-host requires SSH or SMB
	if a.archiveDownloadHost != "" && a.sshUser == "" && a.smbUser == "" {
		return cmdutil.NewUserError("--archive-download-host can only be used with --ssh-user or --smb-user")
	}

	// SSH requires paired options (both or neither)
	if (a.sshUser != "") != (a.sshPrivateKey != "") {
		return cmdutil.NewUserError("Both --ssh-user and --ssh-private-key must be specified for SSH download.")
	}

	// SMB: no additional bidirectional validation needed — C# only checks
	// mutual exclusion with SSH and archive-download-host above.
	// The env fallback for SMB password happens at runtime in the live constructor.

	// Warn if no SSH/SMB specified
	if a.sshUser == "" && a.smbUser == "" {
		log.Warning("You haven't specified --ssh-user or --smb-user, so we assume that you're running the CLI on the Bitbucket instance itself, and export archive will be read from the local filesystem.")
	}

	return nil
}

func validateNoGenerateOptions(a *bbsMigrateRepoArgs) error {
	if a.bbsUsername != "" || a.bbsPassword != "" {
		return cmdutil.NewUserError("--bbs-username and --bbs-password cannot be provided with --archive-path or --archive-url.")
	}

	if a.noSSLVerify {
		return cmdutil.NewUserError("--no-ssl-verify cannot be provided with --archive-path or --archive-url.")
	}

	if a.sshUser != "" || a.sshPrivateKey != "" || a.archiveDownloadHost != "" || a.smbUser != "" || a.smbPassword != "" || a.smbDomain != "" {
		return cmdutil.NewUserError("SSH or SMB download options cannot be provided with --archive-path or --archive-url.")
	}

	return nil
}

func validateBbsUploadOptions(a *bbsMigrateRepoArgs, envProv bbsMigrateRepoEnvProvider) error {
	shouldUseAzure := resolveBbsAzureConnectionString(a.azureStorageConnectionString, envProv) != ""
	shouldUseAWS := a.awsBucketName != ""

	if err := validateBbsUploadConflicts(a, shouldUseAzure, shouldUseAWS); err != nil {
		return err
	}

	if shouldUseAWS {
		return validateBbsAWSCredentials(a, envProv)
	}

	return nil
}

func validateBbsUploadConflicts(a *bbsMigrateRepoArgs, shouldUseAzure, shouldUseAWS bool) error {
	if !shouldUseAWS && hasAWSSubOptions(a) {
		return cmdutil.NewUserError("The AWS S3 bucket name must be provided with --aws-bucket-name if other AWS S3 upload options are set.")
	}
	if a.useGithubStorage && shouldUseAWS {
		return cmdutil.NewUserError("The --use-github-storage flag was provided with an AWS S3 Bucket name. Archive cannot be uploaded to both locations.")
	}
	if shouldUseAzure && a.useGithubStorage {
		return cmdutil.NewUserError("The --use-github-storage flag was provided with a connection string for an Azure storage account. Archive cannot be uploaded to both locations.")
	}
	if !shouldUseAzure && !shouldUseAWS && !a.useGithubStorage {
		return cmdutil.NewUserError(
			"Either Azure storage connection (--azure-storage-connection-string or AZURE_STORAGE_CONNECTION_STRING env. variable) or " +
				"AWS S3 connection (--aws-bucket-name, --aws-access-key (or AWS_ACCESS_KEY_ID env. variable), --aws-secret-key (or AWS_SECRET_ACCESS_KEY env. variable)) or " +
				"GitHub Storage Option (--use-github-storage) " +
				"must be provided.")
	}
	if shouldUseAzure && shouldUseAWS {
		return cmdutil.NewUserError("Azure storage connection and AWS S3 connection cannot be specified together.")
	}
	return nil
}

func hasAWSSubOptions(a *bbsMigrateRepoArgs) bool {
	return a.awsAccessKey != "" ||
		a.awsSecretKey != "" ||
		a.awsSessionToken != "" ||
		a.awsRegion != ""
}

func validateBbsAWSCredentials(a *bbsMigrateRepoArgs, envProv bbsMigrateRepoEnvProvider) error {
	if resolveBbsAWSAccessKey(a.awsAccessKey, envProv) == "" {
		return cmdutil.NewUserError("Either --aws-access-key or AWS_ACCESS_KEY_ID environment variable must be set.")
	}
	if resolveBbsAWSSecretKey(a.awsSecretKey, envProv) == "" {
		return cmdutil.NewUserError("Either --aws-secret-key or AWS_SECRET_ACCESS_KEY environment variable must be set.")
	}
	if resolveBbsAWSRegion(a.awsRegion, envProv) == "" {
		return cmdutil.NewUserError("Either --aws-region or AWS_REGION environment variable must be set.")
	}
	return nil
}

// ---------------------------------------------------------------------------
// Runner
// ---------------------------------------------------------------------------

func runBbsMigrateRepo(
	ctx context.Context,
	a *bbsMigrateRepoArgs,
	gh bbsMigrateRepoGitHub,
	bbsAPI bbsMigrateRepoBbsAPI,
	downloader bbsMigrateRepoArchiveDownloader,
	uploader bbsMigrateRepoArchiveUploader,
	fs bbsMigrateRepoFileSystem,
	envProv bbsMigrateRepoEnvProvider,
	log *logger.Logger,
	opts bbsMigrateRepoOptions,
) error {
	if err := validateBbsMigrateRepoArgs(a, envProv, fs, log); err != nil {
		return err
	}

	log.Info("Migrating Repo...")

	// ---- Phase 0: Pre-checks (if importing) ----
	var migrationSourceID, githubOrgID string
	if a.shouldImportArchive() {
		var err error
		migrationSourceID, githubOrgID, err = bbsPreflightChecks(ctx, a, gh, log)
		if err != nil {
			return err
		}
	}

	// ---- Phase 1 & 2: Generate and download archive ----
	if a.shouldGenerateArchive() {
		if err := bbsGenerateAndDownloadArchive(ctx, a, bbsAPI, downloader, log, opts.exportPollInterval); err != nil {
			return err
		}
	}

	// ---- Phase 3: Upload archive ----
	if a.shouldUploadArchive() {
		if err := bbsUploadArchive(ctx, a, uploader, fs, log); err != nil {
			return err
		}
	}

	// ---- Phase 4: Import (start migration + poll) ----
	if a.shouldImportArchive() {
		if err := bbsImportArchive(ctx, a, gh, envProv, log, migrationSourceID, githubOrgID, opts.pollInterval); err != nil {
			return err
		}
	}

	return nil
}

// bbsGenerateAndDownloadArchive runs the BBS export, polls for completion,
// and optionally downloads the archive via SSH/SMB.
func bbsGenerateAndDownloadArchive(
	ctx context.Context,
	a *bbsMigrateRepoArgs,
	bbsAPI bbsMigrateRepoBbsAPI,
	downloader bbsMigrateRepoArchiveDownloader,
	log *logger.Logger,
	exportPollInterval time.Duration,
) error {
	exportID, err := bbsAPI.StartExport(ctx, a.bbsProject, a.bbsRepo)
	if err != nil {
		return err
	}
	log.Info("Export started with ID: %d", exportID)

	if err := pollBbsExport(ctx, bbsAPI, exportID, log, exportPollInterval); err != nil {
		return err
	}
	log.Info("Export completed successfully.")

	// Download via SSH/SMB if configured
	if a.shouldDownloadArchive() {
		downloadedPath, err := downloader.Download(exportID, ".")
		if err != nil {
			return err
		}
		a.archivePath = downloadedPath
		log.Info("Archive downloaded to %s", downloadedPath)
	} else if a.archivePath == "" {
		// Running on BBS server directly — compute local path
		a.archivePath = bbs.SourceExportArchiveAbsolutePath(a.bbsSharedHome, exportID)
	}

	return nil
}

// bbsPreflightChecks verifies the target repo doesn't exist and creates a migration source.
func bbsPreflightChecks(
	ctx context.Context,
	a *bbsMigrateRepoArgs,
	gh bbsMigrateRepoGitHub,
	_ *logger.Logger,
) (migrationSourceID, githubOrgID string, _ error) {
	exists, err := gh.DoesRepoExist(ctx, a.githubOrg, a.githubRepo)
	if err != nil {
		return "", "", err
	}
	if exists {
		return "", "", cmdutil.NewUserErrorf("A repository called %s/%s already exists", a.githubOrg, a.githubRepo)
	}

	githubOrgID, err = gh.GetOrganizationId(ctx, a.githubOrg)
	if err != nil {
		return "", "", err
	}

	migrationSourceID, err = gh.CreateBbsMigrationSource(ctx, githubOrgID)
	if err != nil {
		if strings.Contains(err.Error(), "not have the correct permissions to execute") {
			msg := fmt.Sprintf("%s%s", err.Error(), bbsInsufficientPermissionsMessage(a.githubOrg))
			return "", "", cmdutil.NewUserError(msg)
		}
		return "", "", err
	}

	return migrationSourceID, githubOrgID, nil
}

// bbsUploadArchive uploads the archive and optionally deletes the local copy.
func bbsUploadArchive(
	ctx context.Context,
	a *bbsMigrateRepoArgs,
	uploader bbsMigrateRepoArchiveUploader,
	fs bbsMigrateRepoFileSystem,
	log *logger.Logger,
) error {
	uploadPath := a.archivePath
	log.Info("Uploading archive from %s ...", uploadPath)

	archiveURL, uploadErr := func() (string, error) {
		fileName := uuid.New().String() + ".tar"
		content, size, err := fs.OpenRead(uploadPath)
		if err != nil {
			return "", fmt.Errorf("opening archive %s: %w", uploadPath, err)
		}
		defer content.Close()

		return uploader.Upload(ctx, a.githubOrg, fileName, content, size)
	}()

	// Delete downloaded archive in a finally-like manner (if downloaded via SSH/SMB)
	if !a.keepArchive && a.shouldDownloadArchive() {
		if err := fs.DeleteIfExists(uploadPath); err != nil {
			log.Warning("Couldn't delete the downloaded archive at '%s': %v", uploadPath, err)
		}
	}

	if uploadErr != nil {
		return uploadErr
	}

	a.archiveURL = archiveURL
	log.Info("Archive uploaded successfully.")
	return nil
}

// bbsImportArchive starts the GitHub migration and polls for completion.
func bbsImportArchive(
	ctx context.Context,
	a *bbsMigrateRepoArgs,
	gh bbsMigrateRepoGitHub,
	envProv bbsMigrateRepoEnvProvider,
	log *logger.Logger,
	migrationSourceID, githubOrgID string,
	pollInterval time.Duration,
) error {
	bbsRepoURL := buildBbsRepoURL(a.bbsServerURL, a.bbsProject, a.bbsRepo)
	targetToken := resolveBbsTargetToken(a.githubPAT, envProv)

	migrationID, err := gh.StartBbsMigration(ctx, migrationSourceID, bbsRepoURL, githubOrgID, a.githubRepo, targetToken, a.archiveURL, a.targetRepoVisibility)
	if err != nil {
		if strings.Contains(err.Error(), fmt.Sprintf("A repository called %s/%s already exists", a.githubOrg, a.githubRepo)) {
			log.Warning("The Org '%s' already contains a repository with the name '%s'. No operation will be performed", a.githubOrg, a.githubRepo)
			return nil
		}
		return err
	}

	if a.queueOnly {
		log.Info("A repository migration (ID: %s) was successfully queued.", migrationID)
		return nil
	}

	m, err := gh.GetMigration(ctx, migrationID)
	if err != nil {
		return err
	}

	for migration.IsRepoPending(m.State) {
		log.Info("Migration in progress (ID: %s). State: %s. Waiting %s...", migrationID, m.State, sharedcmd.FormatPollInterval(pollInterval))

		select {
		case <-ctx.Done():
			return ctx.Err()
		case <-time.After(pollInterval):
		}

		m, err = gh.GetMigration(ctx, migrationID)
		if err != nil {
			return err
		}
	}

	if migration.IsRepoFailed(m.State) {
		log.Errorf("Migration Failed. Migration ID: %s", migrationID)
		sharedcmd.LogWarningsCount(log, m.WarningsCount)
		log.Info("Migration log available at %s or by running `gh bbs2gh download-logs --github-org %s --github-repo %s`", m.MigrationLogURL, a.githubOrg, a.githubRepo)
		return cmdutil.NewUserError(m.FailureReason)
	}

	log.Success("Migration completed (ID: %s)! State: %s", migrationID, m.State)
	sharedcmd.LogWarningsCount(log, m.WarningsCount)
	log.Info("Migration log available at %s or by running `gh bbs2gh download-logs --github-org %s --github-repo %s`", m.MigrationLogURL, a.githubOrg, a.githubRepo)
	return nil
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

func pollBbsExport(ctx context.Context, bbsAPI bbsMigrateRepoBbsAPI, exportID int64, log *logger.Logger, pollInterval time.Duration) error {
	for {
		exportState, message, percentage, err := bbsAPI.GetExport(ctx, exportID)
		if err != nil {
			return err
		}

		log.Info("Export status: %s (%d%%) %s", exportState, percentage, message)

		upper := strings.ToUpper(exportState)

		if upper == "COMPLETED" {
			return nil
		}

		if upper == "FAILED" || upper == "ABORTED" {
			return cmdutil.NewUserErrorf("BBS export failed with state: %s - %s", exportState, message)
		}

		select {
		case <-ctx.Done():
			return ctx.Err()
		case <-time.After(pollInterval):
		}
	}
}

func buildBbsRepoURL(bbsServerURL, project, repo string) string {
	if bbsServerURL != "" && project != "" && repo != "" {
		return fmt.Sprintf("%s/projects/%s/repos/%s/browse", strings.TrimRight(bbsServerURL, "/"), project, repo)
	}
	return "https://not-used"
}

func bbsInsufficientPermissionsMessage(org string) string {
	return fmt.Sprintf(". Please check that:\n  (a) you are a member of the `%s` organization,\n  (b) you are an organization owner or you have been granted the migrator role and\n  (c) your personal access token has the correct scopes.\nFor more information, see https://docs.github.com/en/migrations/using-github-enterprise-importer/preparing-to-migrate-with-github-enterprise-importer/managing-access-for-github-enterprise-importer.", org)
}

func resolveBbsTargetToken(flagValue string, envProv bbsMigrateRepoEnvProvider) string {
	if flagValue != "" {
		return flagValue
	}
	return envProv.TargetGitHubPAT()
}

func resolveBbsAzureConnectionString(flagValue string, envProv bbsMigrateRepoEnvProvider) string {
	if flagValue != "" {
		return flagValue
	}
	return envProv.AzureStorageConnectionString()
}

func resolveBbsAWSAccessKey(flagValue string, envProv bbsMigrateRepoEnvProvider) string {
	if flagValue != "" {
		return flagValue
	}
	return envProv.AWSAccessKeyID()
}

func resolveBbsAWSSecretKey(flagValue string, envProv bbsMigrateRepoEnvProvider) string {
	if flagValue != "" {
		return flagValue
	}
	return envProv.AWSSecretAccessKey()
}

func resolveBbsAWSRegion(flagValue string, envProv bbsMigrateRepoEnvProvider) string {
	if flagValue != "" {
		return flagValue
	}
	return envProv.AWSRegion()
}

// ---------------------------------------------------------------------------
// Adapters for production dependencies
// ---------------------------------------------------------------------------

// bbsFsAdapter wraps filesystem.Provider to satisfy bbsMigrateRepoFileSystem.
type bbsFsAdapter struct {
	prov *filesystem.Provider
}

func (a *bbsFsAdapter) FileExists(path string) bool      { return a.prov.FileExists(path) }
func (a *bbsFsAdapter) DirectoryExists(path string) bool { return a.prov.DirectoryExists(path) }
func (a *bbsFsAdapter) DeleteIfExists(path string) error { return a.prov.DeleteIfExists(path) }
func (a *bbsFsAdapter) OpenRead(path string) (io.ReadSeekCloser, int64, error) {
	return a.prov.OpenRead(path)
}

// bbsEnvProviderAdapter wraps env.Provider to satisfy bbsMigrateRepoEnvProvider.
type bbsEnvProviderAdapter struct {
	prov *env.Provider
}

func (a *bbsEnvProviderAdapter) TargetGitHubPAT() string { return a.prov.TargetGitHubPAT() }
func (a *bbsEnvProviderAdapter) AzureStorageConnectionString() string {
	return a.prov.AzureStorageConnectionString()
}
func (a *bbsEnvProviderAdapter) AWSAccessKeyID() string     { return a.prov.AWSAccessKeyID() }
func (a *bbsEnvProviderAdapter) AWSSecretAccessKey() string { return a.prov.AWSSecretAccessKey() }
func (a *bbsEnvProviderAdapter) AWSSessionToken() string    { return a.prov.AWSSessionToken() }
func (a *bbsEnvProviderAdapter) AWSRegion() string          { return a.prov.AWSRegion() }
func (a *bbsEnvProviderAdapter) BBSUsername() string        { return a.prov.BBSUsername() }
func (a *bbsEnvProviderAdapter) BBSPassword() string        { return a.prov.BBSPassword() }
func (a *bbsEnvProviderAdapter) SmbPassword() string        { return a.prov.SmbPassword() }

// awsLogAdapter adapts *logger.Logger to awsStorage.ProgressLogger (LogInfo method).
type awsLogAdapter struct {
	log *logger.Logger
}

func (a *awsLogAdapter) LogInfo(format string, args ...interface{}) { a.log.Info(format, args...) }

// bbsTokenRoundTripper attaches a Bearer token to every outgoing request.
type bbsTokenRoundTripper struct {
	token string
}

func (t *bbsTokenRoundTripper) RoundTrip(req *http.Request) (*http.Response, error) {
	req = req.Clone(req.Context())
	req.Header.Set("Authorization", "Bearer "+t.token)
	return http.DefaultTransport.RoundTrip(req)
}

// ---------------------------------------------------------------------------
// Production command constructor (used by main.go)
// ---------------------------------------------------------------------------

func newMigrateRepoCmdLive() *cobra.Command {
	var a bbsMigrateRepoArgs

	cmd := &cobra.Command{
		Use:   "migrate-repo",
		Short: "Migrate a Bitbucket Server repository to GitHub",
		Long:  "Exports a Bitbucket Server repository, uploads it to blob storage, and imports it into GitHub using GitHub Enterprise Importer.",
		RunE: func(cmd *cobra.Command, _ []string) error {
			log := getLogger(cmd)
			ctx := cmd.Context()
			envProv := &bbsEnvProviderAdapter{prov: env.New()}
			fsProvider := &bbsFsAdapter{prov: filesystem.New()}

			bbsAPI := buildBbsAPIClient(&a, envProv, log)

			ghClient := buildBbsGitHubClient(&a, envProv, log)

			archiveDownloader, err := buildBbsArchiveDownloader(&a, envProv, log)
			if err != nil {
				return err
			}

			archiveUploader, err := buildBbsArchiveUploader(&a, envProv, log)
			if err != nil {
				return err
			}

			opts := bbsMigrateRepoOptions{
				pollInterval:       bbsMigrationPollInterval,
				exportPollInterval: bbsExportPollInterval,
			}

			return runBbsMigrateRepo(ctx, &a, ghClient, bbsAPI, archiveDownloader, archiveUploader, fsProvider, envProv, log, opts)
		},
	}

	// BBS flags
	cmd.Flags().StringVar(&a.bbsServerURL, "bbs-server-url", "", "The full URL of the Bitbucket Server/Data Center instance")
	cmd.Flags().StringVar(&a.bbsProject, "bbs-project", "", "The Bitbucket Server project key")
	cmd.Flags().StringVar(&a.bbsRepo, "bbs-repo", "", "The Bitbucket Server repository slug")
	cmd.Flags().StringVar(&a.bbsUsername, "bbs-username", "", "Bitbucket Server username (falls back to BBS_USERNAME env)")
	cmd.Flags().StringVar(&a.bbsPassword, "bbs-password", "", "Bitbucket Server password (falls back to BBS_PASSWORD env)")
	cmd.Flags().BoolVar(&a.noSSLVerify, "no-ssl-verify", false, "Disable SSL verification for BBS")
	cmd.Flags().BoolVar(&a.kerberos, "kerberos", false, "Use Kerberos authentication for BBS")

	// SSH download flags
	cmd.Flags().StringVar(&a.sshUser, "ssh-user", "", "SSH username for downloading export archive")
	cmd.Flags().StringVar(&a.sshPrivateKey, "ssh-private-key", "", "Path to SSH private key for downloading export archive")
	cmd.Flags().IntVar(&a.sshPort, "ssh-port", bbsDefaultSSHPort, "SSH port for downloading export archive")

	// SMB download flags
	cmd.Flags().StringVar(&a.smbUser, "smb-user", "", "SMB username for downloading export archive")
	cmd.Flags().StringVar(&a.smbPassword, "smb-password", "", "SMB password (falls back to SMB_PASSWORD env)")
	cmd.Flags().StringVar(&a.smbDomain, "smb-domain", "", "SMB domain for authentication")

	// Archive flags
	cmd.Flags().StringVar(&a.archivePath, "archive-path", "", "Path to a local BBS export archive file")
	cmd.Flags().StringVar(&a.archiveURL, "archive-url", "", "URL to a pre-uploaded BBS export archive")
	cmd.Flags().StringVar(&a.archiveDownloadHost, "archive-download-host", "", "Override host for downloading export archive via SSH/SMB")
	cmd.Flags().StringVar(&a.bbsSharedHome, "bbs-shared-home", bbs.DefaultBbsSharedHomeDirectoryLinux, "Path to Bitbucket Server shared home directory")

	// GitHub target flags
	cmd.Flags().StringVar(&a.githubOrg, "github-org", "", "Target GitHub organization")
	cmd.Flags().StringVar(&a.githubRepo, "github-repo", "", "Target GitHub repository name")
	cmd.Flags().StringVar(&a.githubPAT, "github-pat", "", "Personal access token for the target GitHub instance")
	cmd.Flags().StringVar(&a.targetRepoVisibility, "target-repo-visibility", "", "Target repository visibility (public, private, internal)")
	cmd.Flags().StringVar(&a.targetAPIURL, "target-api-url", bbsDefaultTargetAPIURL, "API URL for the target GitHub instance")
	cmd.Flags().StringVar(&a.targetUploadsURL, "target-uploads-url", "", "Uploads URL for the target GitHub instance")

	// Upload storage flags
	cmd.Flags().StringVar(&a.azureStorageConnectionString, "azure-storage-connection-string", "", "Azure Blob Storage connection string")
	cmd.Flags().StringVar(&a.awsBucketName, "aws-bucket-name", "", "AWS S3 bucket name")
	cmd.Flags().StringVar(&a.awsAccessKey, "aws-access-key", "", "AWS access key (falls back to AWS_ACCESS_KEY_ID env)")
	cmd.Flags().StringVar(&a.awsSecretKey, "aws-secret-key", "", "AWS secret key (falls back to AWS_SECRET_ACCESS_KEY env)")
	cmd.Flags().StringVar(&a.awsSessionToken, "aws-session-token", "", "AWS session token (falls back to AWS_SESSION_TOKEN env)")
	cmd.Flags().StringVar(&a.awsRegion, "aws-region", "", "AWS region (falls back to AWS_REGION env)")

	// Behavior flags
	cmd.Flags().BoolVar(&a.keepArchive, "keep-archive", false, "Keep downloaded archive files after upload")
	cmd.Flags().BoolVar(&a.queueOnly, "queue-only", false, "Queue the migration without waiting for completion")
	cmd.Flags().BoolVar(&a.useGithubStorage, "use-github-storage", false, "Use GitHub-owned storage for archives")

	// Hidden flags
	_ = cmd.Flags().MarkHidden("target-uploads-url")

	return cmd
}

func buildBbsAPIClient(a *bbsMigrateRepoArgs, envProv bbsMigrateRepoEnvProvider, log *logger.Logger) bbsMigrateRepoBbsAPI {
	if a.bbsServerURL == "" {
		return nil
	}
	bbsUser := a.bbsUsername
	if bbsUser == "" {
		bbsUser = envProv.BBSUsername()
	}
	bbsPass := a.bbsPassword
	if bbsPass == "" {
		bbsPass = envProv.BBSPassword()
	}
	return bbs.NewClient(a.bbsServerURL, bbsUser, bbsPass, log)
}

func buildBbsGitHubClient(a *bbsMigrateRepoArgs, envProv bbsMigrateRepoEnvProvider, log *logger.Logger) *github.Client {
	targetToken := resolveBbsTargetToken(a.githubPAT, envProv)
	tgtAPI := a.targetAPIURL
	if tgtAPI == "" {
		tgtAPI = bbsDefaultTargetAPIURL
	}
	return github.NewClient(targetToken,
		github.WithAPIURL(tgtAPI),
		github.WithLogger(log),
		github.WithVersion(version),
	)
}

func resolveBbsDownloadHost(a *bbsMigrateRepoArgs) (string, error) {
	if a.archiveDownloadHost != "" {
		return a.archiveDownloadHost, nil
	}
	if a.bbsServerURL != "" {
		u, err := url.Parse(a.bbsServerURL)
		if err != nil {
			return "", fmt.Errorf("parsing --bbs-server-url: %w", err)
		}
		return u.Hostname(), nil
	}
	return "", nil
}

func buildBbsArchiveDownloader(a *bbsMigrateRepoArgs, envProv bbsMigrateRepoEnvProvider, log *logger.Logger) (bbsMigrateRepoArchiveDownloader, error) {
	if a.sshUser == "" && a.smbUser == "" {
		return nil, nil
	}

	bbsHost, err := resolveBbsDownloadHost(a)
	if err != nil {
		return nil, err
	}

	if a.sshUser != "" {
		dl, err := bbs.NewSSHArchiveDownloader(log, bbsHost, a.sshUser, a.sshPrivateKey, a.sshPort)
		if err != nil {
			return nil, fmt.Errorf("initializing SSH downloader: %w", err)
		}
		dl.BbsSharedHomeDirectory = a.bbsSharedHome
		return dl, nil
	}

	smbPass := a.smbPassword
	if smbPass == "" {
		smbPass = envProv.SmbPassword()
	}
	dl := bbs.NewSMBArchiveDownloader(log, bbsHost, a.smbUser, smbPass, a.smbDomain)
	dl.BbsSharedHomeDirectory = a.bbsSharedHome
	return dl, nil
}

func buildBbsArchiveUploader(a *bbsMigrateRepoArgs, envProv bbsMigrateRepoEnvProvider, log *logger.Logger) (bbsMigrateRepoArchiveUploader, error) {
	uploaderOpts := []archive.UploaderOption{archive.WithLogger(log)}

	azureConnStr := a.azureStorageConnectionString
	if azureConnStr == "" {
		azureConnStr = envProv.AzureStorageConnectionString()
	}
	if azureConnStr != "" {
		azClient, err := azureStorage.NewClient(azureConnStr, log)
		if err != nil {
			return nil, fmt.Errorf("initializing Azure storage client: %w", err)
		}
		uploaderOpts = append(uploaderOpts, archive.WithAzure(azClient))
	}

	if a.awsBucketName != "" {
		awsOpts, err := buildAWSClientOptions(a, envProv, log)
		if err != nil {
			return nil, err
		}
		uploaderOpts = append(uploaderOpts, archive.WithAWS(awsOpts.client, a.awsBucketName))
	}

	if a.useGithubStorage {
		uploadsURL := a.targetUploadsURL
		if uploadsURL == "" {
			uploadsURL = "https://uploads.github.com"
		}

		// Resolve target token for the ghowned HTTP client
		targetToken := resolveBbsTargetToken(a.githubPAT, envProv)

		ghHTTPClient := &http.Client{
			Transport: &bbsTokenRoundTripper{token: targetToken},
		}

		var ghOwnedOpts []ghowned.Option
		ghOwnedOpts = append(ghOwnedOpts, ghowned.WithLogger(log))

		envReal := env.New()
		if mebiStr := envReal.GitHubOwnedStorageMultipartMebibytes(); mebiStr != "" {
			if mebi, err := strconv.ParseInt(mebiStr, 10, 64); err == nil {
				ghOwnedOpts = append(ghOwnedOpts, ghowned.WithPartSizeMebibytes(mebi))
			}
		}

		ghOwnedClient := ghowned.NewClient(uploadsURL, ghHTTPClient, ghOwnedOpts...)

		// Build a GitHub client for org ID resolution
		tgtAPI := a.targetAPIURL
		if tgtAPI == "" {
			tgtAPI = bbsDefaultTargetAPIURL
		}
		targetGH := github.NewClient(targetToken,
			github.WithAPIURL(tgtAPI),
			github.WithLogger(log),
			github.WithVersion(version),
		)

		uploaderOpts = append(uploaderOpts, archive.WithGitHub(ghOwnedClient, targetGH))
	}

	return archive.NewUploader(uploaderOpts...), nil
}

type awsClientResult struct {
	client *awsStorage.Client
}

func buildAWSClientOptions(a *bbsMigrateRepoArgs, envProv bbsMigrateRepoEnvProvider, log *logger.Logger) (*awsClientResult, error) {
	awsAccessKey := a.awsAccessKey
	if awsAccessKey == "" {
		awsAccessKey = envProv.AWSAccessKeyID()
	}
	awsSecretKey := a.awsSecretKey
	if awsSecretKey == "" {
		awsSecretKey = envProv.AWSSecretAccessKey()
	}

	var awsOpts []awsStorage.Option
	awsRegion := a.awsRegion
	if awsRegion == "" {
		awsRegion = envProv.AWSRegion()
	}
	if awsRegion != "" {
		awsOpts = append(awsOpts, awsStorage.WithRegion(awsRegion))
	}
	awsSessionToken := a.awsSessionToken
	if awsSessionToken == "" {
		awsSessionToken = envProv.AWSSessionToken()
	}
	if awsSessionToken != "" {
		awsOpts = append(awsOpts, awsStorage.WithSessionToken(awsSessionToken))
	}
	awsOpts = append(awsOpts, awsStorage.WithLogger(&awsLogAdapter{log: log}))

	client, err := awsStorage.NewClient(awsAccessKey, awsSecretKey, awsOpts...)
	if err != nil {
		return nil, fmt.Errorf("initializing AWS S3 client: %w", err)
	}
	return &awsClientResult{client: client}, nil
}
