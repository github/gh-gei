package main

import (
	"context"
	"fmt"
	"net/url"
	"os"
	"strconv"
	"strings"

	"github.com/github/gh-gei/pkg/env"
	"github.com/github/gh-gei/pkg/github"
	"github.com/github/gh-gei/pkg/http"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/github/gh-gei/pkg/scriptgen"
	"github.com/spf13/cobra"
)

type generateScriptOptions struct {
	githubSourceOrg      string
	githubTargetOrg      string
	output               string
	ghesAPIURL           string
	awsBucketName        string
	awsRegion            string
	noSSLVerify          bool
	skipReleases         bool
	lockSourceRepo       bool
	downloadMigrationLog bool
	sequential           bool
	githubSourcePAT      string
	keepArchive          bool
	targetAPIURL         string
	targetUploadsURL     string
	useGithubStorage     bool
}

func newGenerateScriptCmd() *cobra.Command {
	opts := &generateScriptOptions{}

	cmd := &cobra.Command{
		Use:   "generate-script",
		Short: "Generates a migration script",
		Long: `Generates a migration script. This provides you the ability to review the steps that this tool will take, 
and optionally modify the script if desired before running it.`,
		RunE: func(cmd *cobra.Command, args []string) error {
			log := getLogger(cmd)
			ctx := cmd.Context()
			return runGenerateScript(ctx, opts, log)
		},
	}

	// Required flags
	cmd.Flags().StringVar(&opts.githubSourceOrg, "github-source-org", "", "Source GitHub organization (REQUIRED)")
	cmd.Flags().StringVar(&opts.githubTargetOrg, "github-target-org", "", "Target GitHub organization (REQUIRED)")

	// Optional flags
	cmd.Flags().StringVar(&opts.output, "output", "./migrate.ps1", "Output file path")
	cmd.Flags().StringVar(&opts.ghesAPIURL, "ghes-api-url", "", "API endpoint for GHES instance (e.g., http(s)://myghes.com/api/v3)")
	cmd.Flags().StringVar(&opts.awsBucketName, "aws-bucket-name", "", "S3 bucket name for AWS storage")
	cmd.Flags().StringVar(&opts.awsRegion, "aws-region", "", "AWS region")
	cmd.Flags().BoolVar(&opts.noSSLVerify, "no-ssl-verify", false, "Disable SSL verification for GHES")
	cmd.Flags().BoolVar(&opts.skipReleases, "skip-releases", false, "Skip releases when migrating")
	cmd.Flags().BoolVar(&opts.lockSourceRepo, "lock-source-repo", false, "Lock source repository when migrating")
	cmd.Flags().BoolVar(&opts.downloadMigrationLog, "download-migration-logs", false, "Download migration logs")
	cmd.Flags().BoolVar(&opts.sequential, "sequential", false, "Wait for each migration before starting the next")
	cmd.Flags().StringVar(&opts.githubSourcePAT, "github-source-pat", "", "GitHub source PAT (uses GH_SOURCE_PAT env if not provided)")
	cmd.Flags().BoolVar(&opts.keepArchive, "keep-archive", false, "Keep archive after upload (GHES < 3.8.0)")
	cmd.Flags().StringVar(&opts.targetAPIURL, "target-api-url", "", "Target API URL (defaults to https://api.github.com)")
	cmd.Flags().StringVar(&opts.targetUploadsURL, "target-uploads-url", "", "Target uploads URL")
	cmd.Flags().BoolVar(&opts.useGithubStorage, "use-github-storage", false, "Use GitHub storage for GHES migrations")

	// Mark required flags
	_ = cmd.MarkFlagRequired("github-source-org")
	_ = cmd.MarkFlagRequired("github-target-org")

	return cmd
}

func runGenerateScript(ctx context.Context, opts *generateScriptOptions, log *logger.Logger) error {
	log.Info("Generating Script...")

	// Validate options
	if err := validateGenerateScriptOptions(opts); err != nil {
		return err
	}

	// Get GitHub PAT from environment
	envProvider := env.New()
	githubPAT := opts.githubSourcePAT
	if githubPAT == "" {
		githubPAT = envProvider.SourceGitHubPAT()
		if githubPAT == "" {
			githubPAT = envProvider.TargetGitHubPAT()
		}
	}
	if githubPAT == "" {
		return fmt.Errorf("GH_PAT or GH_SOURCE_PAT environment variable must be set")
	}

	// Create GitHub client for source
	sourceAPIURL := opts.ghesAPIURL
	if sourceAPIURL == "" {
		sourceAPIURL = "https://api.github.com"
	}

	httpCfg := http.DefaultConfig()
	httpCfg.NoSSLVerify = opts.noSSLVerify
	httpClient := http.NewClient(httpCfg, log)

	githubCfg := github.Config{
		APIURL:      sourceAPIURL,
		PAT:         githubPAT,
		NoSSLVerify: opts.noSSLVerify,
	}
	githubClient := github.NewClient(githubCfg, httpClient, log)

	// Get repositories from source org
	log.Info("GITHUB ORG: %s", opts.githubSourceOrg)
	repos, err := githubClient.GetRepos(ctx, opts.githubSourceOrg)
	if err != nil {
		return fmt.Errorf("failed to get repositories: %w", err)
	}

	if len(repos) == 0 {
		return fmt.Errorf("a migration script could not be generated because no migratable repos were found")
	}

	for _, repo := range repos {
		log.Info("    Repo: %s", repo.Name)
	}

	// Check if blob credentials are required (GHES < 3.8.0)
	blobCredentialsRequired := false
	if opts.ghesAPIURL != "" {
		blobCredentialsRequired = true
		log.Info("Using GitHub Enterprise Server - verifying server version")

		versionInfo, err := githubClient.GetVersion(ctx)
		if err == nil && versionInfo != nil && versionInfo.Version != "" {
			log.Info("GitHub Enterprise Server version %s detected", versionInfo.Version)
			// Parse version and check if < 3.8.0
			if isGHESVersionAtLeast(versionInfo.Version, 3, 8, 0) {
				blobCredentialsRequired = false
			}
		} else {
			log.Info("Unable to parse the version number, defaulting to using CLI for blob storage uploads")
		}
	}

	// Convert github.Repo to scriptgen.Repository
	scriptRepos := make([]scriptgen.Repository, len(repos))
	for i, repo := range repos {
		scriptRepos[i] = scriptgen.Repository{
			Name:       repo.Name,
			Visibility: repo.Visibility,
		}
	}

	// Generate script using scriptgen package
	genOpts := scriptgen.GeneratorOptions{
		SourceOrg:               opts.githubSourceOrg,
		TargetOrg:               opts.githubTargetOrg,
		Sequential:              opts.sequential,
		Verbose:                 log.IsVerbose(),
		SkipReleases:            opts.skipReleases,
		LockSourceRepo:          opts.lockSourceRepo,
		DownloadMigrationLog:    opts.downloadMigrationLog,
		TargetAPIURL:            opts.targetAPIURL,
		TargetUploadsURL:        opts.targetUploadsURL,
		GHESAPIUrl:              opts.ghesAPIURL,
		AWSBucketName:           opts.awsBucketName,
		AWSRegion:               opts.awsRegion,
		NoSSLVerify:             opts.noSSLVerify,
		KeepArchive:             opts.keepArchive,
		UseGithubStorage:        opts.useGithubStorage,
		BlobCredentialsRequired: blobCredentialsRequired,
		CLIVersion:              version,
		CLICommand:              "gh gei",
	}

	generator := scriptgen.NewGenerator(genOpts, scriptRepos)
	script := generator.Generate()

	// Write script to file
	if err := os.WriteFile(opts.output, []byte(script), 0755); err != nil {
		return fmt.Errorf("failed to write script: %w", err)
	}

	log.Success("Script generated successfully: %s", opts.output)
	return nil
}

func validateGenerateScriptOptions(opts *generateScriptOptions) error {
	// Check if org names are URLs
	if strings.Contains(opts.githubSourceOrg, "://") || strings.HasPrefix(opts.githubSourceOrg, "http") {
		return fmt.Errorf("--github-source-org expects an organization name, not a URL. Please provide just the organization name (e.g., 'my-org' instead of 'https://github.com/my-org')")
	}
	if strings.Contains(opts.githubTargetOrg, "://") || strings.HasPrefix(opts.githubTargetOrg, "http") {
		return fmt.Errorf("--github-target-org expects an organization name, not a URL. Please provide just the organization name (e.g., 'my-org' instead of 'https://github.com/my-org')")
	}

	// Validate AWS bucket name requirements
	if opts.awsBucketName != "" {
		if opts.ghesAPIURL == "" {
			return fmt.Errorf("--ghes-api-url must be specified when --aws-bucket-name is specified")
		}
		if opts.useGithubStorage {
			return fmt.Errorf("the --use-github-storage flag was provided with an AWS S3 Bucket name. Archive cannot be uploaded to both locations")
		}
	}

	// Validate no-ssl-verify requirements
	if opts.noSSLVerify && opts.ghesAPIURL == "" {
		return fmt.Errorf("--ghes-api-url must be specified when --no-ssl-verify is specified")
	}

	// Validate use-github-storage requirements
	if opts.useGithubStorage && opts.ghesAPIURL == "" {
		return fmt.Errorf("--ghes-api-url must be specified when --use-github-storage is specified")
	}

	// Validate GHES API URL format
	if opts.ghesAPIURL != "" {
		if _, err := url.ParseRequestURI(opts.ghesAPIURL); err != nil {
			return fmt.Errorf("--ghes-api-url is invalid. Please check URL before trying again")
		}
	}

	return nil
}

func isGHESVersionAtLeast(versionStr string, major, minor, patch int) bool {
	// Simple version parsing - extract first three numeric components
	parts := strings.Split(versionStr, ".")
	if len(parts) < 3 {
		return false
	}

	vmajor, err := strconv.Atoi(parts[0])
	if err != nil {
		return false
	}
	vminor, err := strconv.Atoi(parts[1])
	if err != nil {
		return false
	}
	vpatch, err := strconv.Atoi(parts[2])
	if err != nil {
		return false
	}

	if vmajor > major {
		return true
	}
	if vmajor == major && vminor > minor {
		return true
	}
	if vmajor == major && vminor == minor && vpatch >= patch {
		return true
	}
	return false
}
