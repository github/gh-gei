package main

import (
	"context"
	"fmt"
	"os"
	"regexp"
	"strings"

	"github.com/github/gh-gei/pkg/bbs"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/github/gh-gei/pkg/scriptgen"
	"github.com/spf13/cobra"
)

// ---------------------------------------------------------------------------
// Consumer-defined interfaces
// ---------------------------------------------------------------------------

// generateScriptBbsAPI defines the BBS API methods used by generate-script.
type generateScriptBbsAPI interface {
	GetProjects(ctx context.Context) ([]genScriptProject, error)
	GetRepos(ctx context.Context, projectKey string) ([]genScriptRepository, error)
}

// genScriptProject mirrors bbs.Project for the consumer-defined interface.
type genScriptProject struct {
	ID   int
	Key  string
	Name string
}

// genScriptRepository mirrors bbs.Repository for the consumer-defined interface.
type genScriptRepository struct {
	ID   int
	Slug string
	Name string
}

// ---------------------------------------------------------------------------
// BBS-specific validation constants (matching C# inline strings exactly)
// ---------------------------------------------------------------------------

const bbsValidateBBSUsername = `
if (-not $env:BBS_USERNAME) {
    Write-Error "BBS_USERNAME environment variable must be set to a valid user that will be used to call Bitbucket Server/Data Center API's to generate a migration archive."
    exit 1
} else {
    Write-Host "BBS_USERNAME environment variable is set and will be used to authenticate to Bitbucket Server/Data Center APIs."
}`

//nolint:gosec // G101 false positive: not credentials; PowerShell validation template
const bbsValidateBBSPassword = `
if (-not $env:BBS_PASSWORD) {
    Write-Error "BBS_PASSWORD environment variable must be set to a valid password that will be used to call Bitbucket Server/Data Center API's to generate a migration archive."
    exit 1
} else {
    Write-Host "BBS_PASSWORD environment variable is set and will be used to authenticate to Bitbucket Server/Data Center APIs."
}`

//nolint:gosec // G101 false positive: not credentials; PowerShell validation template
const bbsValidateSMBPassword = `
if (-not $env:SMB_PASSWORD) {
    Write-Error "SMB_PASSWORD environment variable must be set to a valid password that will be used to download the migration archive from your BBS server using SMB."
    exit 1
} else {
    Write-Host "SMB_PASSWORD environment variable is set and will be used to download the migration archive from your BBS server using SMB."
}`

// ---------------------------------------------------------------------------
// Args struct
// ---------------------------------------------------------------------------

type bbsGenerateScriptArgs struct {
	bbsServerURL        string
	githubOrg           string
	bbsUsername         string
	bbsPassword         string
	bbsProject          string
	bbsSharedHome       string
	archiveDownloadHost string
	sshUser             string
	sshPrivateKey       string
	sshPort             int
	smbUser             string
	smbDomain           string
	output              string
	kerberos            bool
	verbose             bool
	awsBucketName       string
	awsRegion           string
	keepArchive         bool
	noSslVerify         bool
	targetAPIURL        string
	targetUploadsURL    string
	useGithubStorage    bool
}

// ---------------------------------------------------------------------------
// Command constructor (testable)
// ---------------------------------------------------------------------------

func newGenerateScriptCmd(
	api generateScriptBbsAPI,
	cliVersion string,
	log *logger.Logger,
	writeToFile func(path, content string) error,
) *cobra.Command {
	var a bbsGenerateScriptArgs

	cmd := &cobra.Command{
		Use:   "generate-script",
		Short: "Generates a migration script",
		Long:  "Generates a PowerShell script that automates a Bitbucket Server to GitHub migration.",
		RunE: func(cmd *cobra.Command, _ []string) error {
			return runBbsGenerateScript(cmd.Context(), api, cliVersion, log, a, writeToFile)
		},
	}

	// Required flags
	cmd.Flags().StringVar(&a.bbsServerURL, "bbs-server-url", "", "The URL of the Bitbucket Server/Data Center instance (REQUIRED)")
	cmd.Flags().StringVar(&a.githubOrg, "github-org", "", "Target GitHub organization name (REQUIRED)")

	// Optional flags
	cmd.Flags().StringVar(&a.bbsUsername, "bbs-username", "", "BBS username for API authentication")
	cmd.Flags().StringVar(&a.bbsPassword, "bbs-password", "", "BBS password for API authentication")
	cmd.Flags().StringVar(&a.bbsProject, "bbs-project", "", "Only migrate repos from this BBS project")
	cmd.Flags().StringVar(&a.bbsSharedHome, "bbs-shared-home", "", "BBS shared home directory")
	cmd.Flags().StringVar(&a.sshUser, "ssh-user", "", "SSH user for archive download")
	cmd.Flags().StringVar(&a.sshPrivateKey, "ssh-private-key", "", "Path to SSH private key")
	cmd.Flags().IntVar(&a.sshPort, "ssh-port", 22, "SSH port")
	cmd.Flags().StringVar(&a.archiveDownloadHost, "archive-download-host", "", "Host for archive download")
	cmd.Flags().StringVar(&a.smbUser, "smb-user", "", "SMB user for archive download")
	cmd.Flags().StringVar(&a.smbDomain, "smb-domain", "", "SMB domain")
	cmd.Flags().StringVar(&a.output, "output", "./migrate.ps1", "Output file path for the migration script")
	cmd.Flags().BoolVar(&a.kerberos, "kerberos", false, "Use Kerberos authentication")
	cmd.Flags().BoolVar(&a.verbose, "verbose", false, "Include verbose flag in generated script commands")
	cmd.Flags().StringVar(&a.awsBucketName, "aws-bucket-name", "", "AWS S3 bucket name")
	cmd.Flags().StringVar(&a.awsRegion, "aws-region", "", "AWS S3 region")
	cmd.Flags().BoolVar(&a.keepArchive, "keep-archive", false, "Keep the migration archive after upload")
	cmd.Flags().BoolVar(&a.noSslVerify, "no-ssl-verify", false, "Disable SSL verification for BBS API calls")
	cmd.Flags().StringVar(&a.targetAPIURL, "target-api-url", "", "API URL for the target GitHub instance")
	cmd.Flags().StringVar(&a.targetUploadsURL, "target-uploads-url", "", "Uploads URL for the target GitHub instance")
	cmd.Flags().BoolVar(&a.useGithubStorage, "use-github-storage", false, "Use GitHub storage for migration archives")

	// Hidden flags
	_ = cmd.Flags().MarkHidden("kerberos")
	_ = cmd.Flags().MarkHidden("use-github-storage")

	return cmd
}

// ---------------------------------------------------------------------------
// Production command constructor
// ---------------------------------------------------------------------------

func newGenerateScriptCmdLive() *cobra.Command {
	var a bbsGenerateScriptArgs

	cmd := &cobra.Command{
		Use:   "generate-script",
		Short: "Generates a migration script",
		Long:  "Generates a PowerShell script that automates a Bitbucket Server to GitHub migration.",
		RunE: func(cmd *cobra.Command, _ []string) error {
			log := getLogger(cmd)

			bbsUser := a.bbsUsername
			if bbsUser == "" {
				bbsUser = getEnvProvider().BBSUsername()
			}
			bbsPass := a.bbsPassword
			if bbsPass == "" {
				bbsPass = getEnvProvider().BBSPassword()
			}

			bbsClient := bbs.NewClient(a.bbsServerURL, bbsUser, bbsPass, log)
			api := &genScriptBbsClientAdapter{client: bbsClient}

			writeToFile := func(path, content string) error {
				return os.WriteFile(path, []byte(content), 0o600)
			}

			return runBbsGenerateScript(cmd.Context(), api, version, log, a, writeToFile)
		},
	}

	// Required flags
	cmd.Flags().StringVar(&a.bbsServerURL, "bbs-server-url", "", "The URL of the Bitbucket Server/Data Center instance (REQUIRED)")
	cmd.Flags().StringVar(&a.githubOrg, "github-org", "", "Target GitHub organization name (REQUIRED)")

	// Optional flags
	cmd.Flags().StringVar(&a.bbsUsername, "bbs-username", "", "BBS username for API authentication")
	cmd.Flags().StringVar(&a.bbsPassword, "bbs-password", "", "BBS password for API authentication")
	cmd.Flags().StringVar(&a.bbsProject, "bbs-project", "", "Only migrate repos from this BBS project")
	cmd.Flags().StringVar(&a.bbsSharedHome, "bbs-shared-home", "", "BBS shared home directory")
	cmd.Flags().StringVar(&a.sshUser, "ssh-user", "", "SSH user for archive download")
	cmd.Flags().StringVar(&a.sshPrivateKey, "ssh-private-key", "", "Path to SSH private key")
	cmd.Flags().IntVar(&a.sshPort, "ssh-port", 22, "SSH port")
	cmd.Flags().StringVar(&a.archiveDownloadHost, "archive-download-host", "", "Host for archive download")
	cmd.Flags().StringVar(&a.smbUser, "smb-user", "", "SMB user for archive download")
	cmd.Flags().StringVar(&a.smbDomain, "smb-domain", "", "SMB domain")
	cmd.Flags().StringVar(&a.output, "output", "./migrate.ps1", "Output file path for the migration script")
	cmd.Flags().BoolVar(&a.kerberos, "kerberos", false, "Use Kerberos authentication")
	cmd.Flags().BoolVar(&a.verbose, "verbose", false, "Include verbose flag in generated script commands")
	cmd.Flags().StringVar(&a.awsBucketName, "aws-bucket-name", "", "AWS S3 bucket name")
	cmd.Flags().StringVar(&a.awsRegion, "aws-region", "", "AWS S3 region")
	cmd.Flags().BoolVar(&a.keepArchive, "keep-archive", false, "Keep the migration archive after upload")
	cmd.Flags().BoolVar(&a.noSslVerify, "no-ssl-verify", false, "Disable SSL verification for BBS API calls")
	cmd.Flags().StringVar(&a.targetAPIURL, "target-api-url", "", "API URL for the target GitHub instance")
	cmd.Flags().StringVar(&a.targetUploadsURL, "target-uploads-url", "", "Uploads URL for the target GitHub instance")
	cmd.Flags().BoolVar(&a.useGithubStorage, "use-github-storage", false, "Use GitHub storage for migration archives")

	// Hidden flags
	_ = cmd.Flags().MarkHidden("kerberos")
	_ = cmd.Flags().MarkHidden("use-github-storage")

	return cmd
}

// genScriptBbsClientAdapter adapts *bbs.Client to the generateScriptBbsAPI interface.
type genScriptBbsClientAdapter struct {
	client *bbs.Client
}

func (a *genScriptBbsClientAdapter) GetProjects(ctx context.Context) ([]genScriptProject, error) {
	projects, err := a.client.GetProjects(ctx)
	if err != nil {
		return nil, err
	}
	result := make([]genScriptProject, len(projects))
	for i, p := range projects {
		result[i] = genScriptProject{ID: p.ID, Key: p.Key, Name: p.Name}
	}
	return result, nil
}

func (a *genScriptBbsClientAdapter) GetRepos(ctx context.Context, projectKey string) ([]genScriptRepository, error) {
	repos, err := a.client.GetRepos(ctx, projectKey)
	if err != nil {
		return nil, err
	}
	result := make([]genScriptRepository, len(repos))
	for i, r := range repos {
		result[i] = genScriptRepository{ID: r.ID, Slug: r.Slug, Name: r.Name}
	}
	return result, nil
}

// ---------------------------------------------------------------------------
// Runner
// ---------------------------------------------------------------------------

func runBbsGenerateScript(
	ctx context.Context,
	api generateScriptBbsAPI,
	cliVersion string,
	log *logger.Logger,
	a bbsGenerateScriptArgs,
	writeToFile func(path, content string) error,
) error {
	// Validate args
	if a.noSslVerify && strings.TrimSpace(a.bbsServerURL) == "" {
		return fmt.Errorf("--no-ssl-verify can only be provided with --bbs-server-url")
	}
	if strings.TrimSpace(a.awsBucketName) != "" && a.useGithubStorage {
		return fmt.Errorf("the --use-github-storage flag was provided with an AWS S3 Bucket name. Archive cannot be uploaded to both locations")
	}
	if strings.TrimSpace(a.awsRegion) != "" && a.useGithubStorage {
		return fmt.Errorf("the --use-github-storage flag was provided with an AWS S3 region. Archive cannot be uploaded to both locations")
	}
	if a.sshPort == 7999 {
		log.Warning("--ssh-port is set to 7999, which is the default Bitbucket Server SSH clone port. This is usually not the port used for SSH archive download. Please verify that this is the correct port.")
	}

	log.Info("Generating Script...")

	script, err := generateBbsScript(ctx, api, cliVersion, log, a)
	if err != nil {
		return err
	}

	if strings.TrimSpace(script) != "" && strings.TrimSpace(a.output) != "" {
		return writeToFile(a.output, script)
	}

	return nil
}

// ---------------------------------------------------------------------------
// Script generation
// ---------------------------------------------------------------------------

func generateBbsScript(
	ctx context.Context,
	api generateScriptBbsAPI,
	cliVersion string,
	log *logger.Logger,
	a bbsGenerateScriptArgs,
) (string, error) {
	var sb strings.Builder

	bbsAppendLine(&sb, scriptgen.PwshShebang)
	bbsAppendBlankLine(&sb)
	bbsAppendLine(&sb, bbsVersionComment(cliVersion))
	bbsAppendLine(&sb, scriptgen.ExecFunctionBlock)

	bbsAppendLine(&sb, scriptgen.ValidateGHPAT)
	if !a.kerberos {
		bbsAppendLine(&sb, bbsValidateBBSPassword)
	}
	if strings.TrimSpace(a.bbsUsername) == "" && !a.kerberos {
		bbsAppendLine(&sb, bbsValidateBBSUsername)
	}
	if strings.TrimSpace(a.awsBucketName) != "" || strings.TrimSpace(a.awsRegion) != "" {
		bbsAppendLine(&sb, scriptgen.ValidateAWSAccessKeyID)
		bbsAppendLine(&sb, scriptgen.ValidateAWSSecretAccessKey)
	} else if !a.useGithubStorage {
		bbsAppendLine(&sb, scriptgen.ValidateAzureStorageConnectionString)
	}
	if strings.TrimSpace(a.smbUser) != "" {
		bbsAppendLine(&sb, bbsValidateSMBPassword)
	}

	var projectKeys []string
	if strings.TrimSpace(a.bbsProject) != "" {
		projectKeys = []string{a.bbsProject}
	} else {
		projects, err := api.GetProjects(ctx)
		if err != nil {
			return "", err
		}
		for _, p := range projects {
			projectKeys = append(projectKeys, p.Key)
		}
	}

	for _, projectKey := range projectKeys {
		log.Info("Project: %s", projectKey)

		bbsAppendBlankLine(&sb)
		bbsAppendLine(&sb, fmt.Sprintf("# =========== Project: %s ===========", projectKey))

		repos, err := api.GetRepos(ctx, projectKey)
		if err != nil {
			return "", err
		}

		if len(repos) == 0 {
			bbsAppendLine(&sb, "# Skipping this project because it has no git repos.")
			continue
		}

		bbsAppendBlankLine(&sb)

		for _, repo := range repos {
			log.Info("  Repo: %s", repo.Name)

			bbsAppendLine(&sb, bbsExecWrap(bbsMigrateRepoScript(a, projectKey, repo.Slug)))
		}
	}

	return sb.String(), nil
}

// ---------------------------------------------------------------------------
// Migrate repo script command
// ---------------------------------------------------------------------------

func bbsMigrateRepoScript(a bbsGenerateScriptArgs, bbsProjectKey, bbsRepoSlug string) string {
	var sb strings.Builder
	sb.WriteString("gh bbs2gh migrate-repo")

	bbsWriteTargetOptions(&sb, a)
	fmt.Fprintf(&sb, ` --bbs-server-url "%s"`, a.bbsServerURL)
	if strings.TrimSpace(a.bbsUsername) != "" {
		fmt.Fprintf(&sb, ` --bbs-username "%s"`, a.bbsUsername)
	}
	if strings.TrimSpace(a.bbsSharedHome) != "" {
		fmt.Fprintf(&sb, ` --bbs-shared-home "%s"`, a.bbsSharedHome)
	}
	fmt.Fprintf(&sb, ` --bbs-project "%s"`, bbsProjectKey)
	fmt.Fprintf(&sb, ` --bbs-repo "%s"`, bbsRepoSlug)

	bbsWriteSSHOptions(&sb, a)
	bbsWriteSMBOptions(&sb, a)

	fmt.Fprintf(&sb, ` --github-org "%s"`, a.githubOrg)
	fmt.Fprintf(&sb, ` --github-repo "%s"`, bbsGetGithubRepoName(bbsProjectKey, bbsRepoSlug))

	bbsWriteTrailingFlags(&sb, a)

	return sb.String()
}

func bbsWriteTargetOptions(sb *strings.Builder, a bbsGenerateScriptArgs) {
	if strings.TrimSpace(a.targetAPIURL) != "" {
		fmt.Fprintf(sb, ` --target-api-url "%s"`, a.targetAPIURL)
	}
	if strings.TrimSpace(a.targetUploadsURL) != "" {
		fmt.Fprintf(sb, ` --target-uploads-url "%s"`, a.targetUploadsURL)
	}
}

func bbsWriteSSHOptions(sb *strings.Builder, a bbsGenerateScriptArgs) {
	if strings.TrimSpace(a.sshUser) == "" {
		return
	}
	fmt.Fprintf(sb, ` --ssh-user "%s" --ssh-private-key "%s"`, a.sshUser, a.sshPrivateKey)
	if a.sshPort != 0 {
		fmt.Fprintf(sb, " --ssh-port %d", a.sshPort)
	}
	if strings.TrimSpace(a.archiveDownloadHost) != "" {
		fmt.Fprintf(sb, " --archive-download-host %s", a.archiveDownloadHost)
	}
}

func bbsWriteSMBOptions(sb *strings.Builder, a bbsGenerateScriptArgs) {
	if strings.TrimSpace(a.smbUser) == "" {
		return
	}
	fmt.Fprintf(sb, ` --smb-user "%s"`, a.smbUser)
	if strings.TrimSpace(a.smbDomain) != "" {
		fmt.Fprintf(sb, " --smb-domain %s", a.smbDomain)
	}
	if strings.TrimSpace(a.archiveDownloadHost) != "" {
		fmt.Fprintf(sb, " --archive-download-host %s", a.archiveDownloadHost)
	}
}

func bbsWriteTrailingFlags(sb *strings.Builder, a bbsGenerateScriptArgs) {
	if a.verbose {
		sb.WriteString(" --verbose")
	}
	// wait is always true in generate-script, so waitOption is always empty
	if a.kerberos {
		sb.WriteString(" --kerberos")
	}
	if strings.TrimSpace(a.awsBucketName) != "" {
		fmt.Fprintf(sb, ` --aws-bucket-name "%s"`, a.awsBucketName)
	}
	if strings.TrimSpace(a.awsRegion) != "" {
		fmt.Fprintf(sb, ` --aws-region "%s"`, a.awsRegion)
	}
	if a.keepArchive {
		sb.WriteString(" --keep-archive")
	}
	if a.noSslVerify {
		sb.WriteString(" --no-ssl-verify")
	}
	sb.WriteString(" --target-repo-visibility private")
	if a.useGithubStorage {
		sb.WriteString(" --use-github-storage")
	}
}

// ---------------------------------------------------------------------------
// String helpers
// ---------------------------------------------------------------------------

var bbsInvalidCharsRe = regexp.MustCompile(`[^\w.\-]+`)

func bbsReplaceInvalidCharactersWithDash(s string) string {
	return bbsInvalidCharsRe.ReplaceAllString(s, "-")
}

func bbsGetGithubRepoName(bbsProjectKey, bbsRepoSlug string) string {
	return bbsReplaceInvalidCharactersWithDash(bbsProjectKey + "-" + bbsRepoSlug)
}

func bbsVersionComment(cliVersion string) string {
	return fmt.Sprintf("# =========== Created with CLI version %s ===========", cliVersion)
}

// bbsAppendLine appends content + newline, but SKIPS if content is empty/whitespace.
func bbsAppendLine(sb *strings.Builder, content string) {
	if strings.TrimSpace(content) == "" {
		return
	}
	sb.WriteString(content)
	sb.WriteByte('\n')
}

// bbsAppendBlankLine always appends a newline.
func bbsAppendBlankLine(sb *strings.Builder) {
	sb.WriteByte('\n')
}

// bbsExecWrap wraps a script in "Exec { ... }". Returns "" if script is empty.
func bbsExecWrap(script string) string {
	if strings.TrimSpace(script) == "" {
		return ""
	}
	return fmt.Sprintf("Exec { %s }", script)
}

// bbsDefaultWriteToFile writes content to a file (production implementation).
func bbsDefaultWriteToFile(path, content string) error { //nolint:unused // will be used when newGenerateScriptCmdLive is fully wired
	return os.WriteFile(path, []byte(content), 0o600)
}
