package scriptgen

import (
	"fmt"
	"strings"
)

// Repository represents a repository to be migrated
type Repository struct {
	Name       string
	Visibility string
}

// GeneratorOptions contains options for script generation
type GeneratorOptions struct {
	// Common options
	SourceOrg            string
	TargetOrg            string
	Sequential           bool
	Verbose              bool
	SkipReleases         bool
	LockSourceRepo       bool
	DownloadMigrationLog bool
	TargetAPIURL         string
	TargetUploadsURL     string

	// GHES-specific options
	GHESAPIUrl       string
	AWSBucketName    string
	AWSRegion        string
	NoSSLVerify      bool
	KeepArchive      bool
	UseGithubStorage bool

	// GHES version checking
	BlobCredentialsRequired bool

	// CLI version
	CLIVersion string

	// CLI command prefix (e.g., "gh gei", "gh ado2gh", "gh bbs2gh")
	CLICommand string

	// ADO-specific options
	ADOOrg         string
	ADOTeamProject string

	// BBS-specific options
	BBSServerURL string
	BBSProject   string
}

// Generator generates PowerShell migration scripts
type Generator struct {
	options GeneratorOptions
	repos   []Repository
}

// NewGenerator creates a new script generator
func NewGenerator(options GeneratorOptions, repos []Repository) *Generator {
	return &Generator{
		options: options,
		repos:   repos,
	}
}

// Generate generates the migration script based on options
func (g *Generator) Generate() string {
	if g.options.Sequential {
		return g.generateSequentialScript()
	}
	return g.generateParallelScript()
}

// generateSequentialScript generates a sequential migration script
func (g *Generator) generateSequentialScript() string {
	var sb strings.Builder

	// Header
	sb.WriteString(PwshShebang + "\n")
	sb.WriteString("\n")
	sb.WriteString(g.versionComment() + "\n")
	sb.WriteString(ExecFunctionBlock + "\n")

	// Validation blocks
	g.writeValidationBlocks(&sb)

	sb.WriteString(fmt.Sprintf("# =========== Organization: %s ===========\n", g.options.SourceOrg))

	// Generate migration commands for each repo
	for _, repo := range g.repos {
		migrateCmd := g.buildMigrateRepoCommand(repo, true)
		sb.WriteString(fmt.Sprintf("Exec { %s }\n", migrateCmd))

		if g.options.DownloadMigrationLog {
			downloadCmd := g.buildDownloadLogsCommand(repo.Name)
			sb.WriteString(fmt.Sprintf("Exec { %s }\n", downloadCmd))
		}
	}

	return sb.String()
}

// generateParallelScript generates a parallel migration script
func (g *Generator) generateParallelScript() string {
	var sb strings.Builder

	// Header
	sb.WriteString(PwshShebang + "\n")
	sb.WriteString("\n")
	sb.WriteString(g.versionComment() + "\n")
	sb.WriteString(ExecAndGetMigrationIDFunctionBlock + "\n")

	// Validation blocks
	g.writeValidationBlocks(&sb)

	// Initialize counters
	sb.WriteString("\n")
	sb.WriteString("$Succeeded = 0\n")
	sb.WriteString("$Failed = 0\n")
	sb.WriteString("$RepoMigrations = [ordered]@{}\n")
	sb.WriteString("\n")
	sb.WriteString(fmt.Sprintf("# =========== Organization: %s ===========\n", g.options.SourceOrg))
	sb.WriteString("\n")
	sb.WriteString("# === Queuing repo migrations ===\n")

	// Queue all migrations
	for _, repo := range g.repos {
		migrateCmd := g.buildMigrateRepoCommand(repo, false)
		sb.WriteString(fmt.Sprintf("$MigrationID = ExecAndGetMigrationID { %s }\n", migrateCmd))
		sb.WriteString(fmt.Sprintf("$RepoMigrations[\"%s\"] = $MigrationID\n", repo.Name))
		sb.WriteString("\n")
	}

	// Wait for all migrations
	sb.WriteString("\n")
	sb.WriteString(fmt.Sprintf("# =========== Waiting for all migrations to finish for Organization: %s ===========\n", g.options.SourceOrg))
	sb.WriteString("\n")

	for _, repo := range g.repos {
		waitCmd := g.buildWaitForMigrationCommand(repo.Name)
		sb.WriteString(fmt.Sprintf("if ($RepoMigrations[\"%s\"]) { %s }\n", repo.Name, waitCmd))
		sb.WriteString(fmt.Sprintf("if ($RepoMigrations[\"%s\"] -and $lastexitcode -eq 0) { $Succeeded++ } else { $Failed++ }\n", repo.Name))

		if g.options.DownloadMigrationLog {
			downloadCmd := g.buildDownloadLogsCommand(repo.Name)
			sb.WriteString(fmt.Sprintf("%s\n", downloadCmd))
		}

		sb.WriteString("\n")
	}

	// Summary
	sb.WriteString("\n")
	sb.WriteString("Write-Host =============== Summary ===============\n")
	sb.WriteString("Write-Host Total number of successful migrations: $Succeeded\n")
	sb.WriteString("Write-Host Total number of failed migrations: $Failed\n")
	sb.WriteString("\n")
	sb.WriteString("if ($Failed -ne 0) {\n")
	sb.WriteString("    exit 1\n")
	sb.WriteString("}\n")
	sb.WriteString("\n")
	sb.WriteString("\n")

	return sb.String()
}

// writeValidationBlocks writes environment variable validation blocks
func (g *Generator) writeValidationBlocks(sb *strings.Builder) {
	sb.WriteString(ValidateGHPAT)
	sb.WriteString("\n")

	// Add source-specific PAT validation
	if g.options.ADOOrg != "" {
		sb.WriteString(ValidateADOPAT)
		sb.WriteString("\n")
	}
	if g.options.BBSServerURL != "" {
		sb.WriteString(ValidateBBSUsername)
		sb.WriteString("\n")
		sb.WriteString(ValidateBBSPassword)
		sb.WriteString("\n")
	}

	// Add storage validation if blob credentials are required
	if !g.options.UseGithubStorage && g.options.BlobCredentialsRequired {
		if g.options.AWSBucketName != "" || g.options.AWSRegion != "" {
			sb.WriteString(ValidateAWSAccessKeyID)
			sb.WriteString("\n")
			sb.WriteString(ValidateAWSSecretAccessKey)
			sb.WriteString("\n")
		} else {
			sb.WriteString(ValidateAzureStorageConnectionString)
			sb.WriteString("\n")
		}
	}
}

// buildMigrateRepoCommand builds the migrate-repo command
func (g *Generator) buildMigrateRepoCommand(repo Repository, wait bool) string {
	var parts []string

	parts = append(parts, g.options.CLICommand+" migrate-repo")

	if g.options.TargetAPIURL != "" {
		parts = append(parts, fmt.Sprintf(`--target-api-url "%s"`, g.options.TargetAPIURL))
	}
	if g.options.TargetUploadsURL != "" {
		parts = append(parts, fmt.Sprintf(`--target-uploads-url "%s"`, g.options.TargetUploadsURL))
	}

	// Add source-specific options
	if g.options.ADOOrg != "" {
		parts = append(parts, fmt.Sprintf(`--ado-org "%s"`, g.options.ADOOrg))
		parts = append(parts, fmt.Sprintf(`--ado-team-project "%s"`, g.options.ADOTeamProject))
		parts = append(parts, fmt.Sprintf(`--ado-repo "%s"`, repo.Name))
	} else if g.options.BBSServerURL != "" {
		parts = append(parts, fmt.Sprintf(`--bbs-server-url "%s"`, g.options.BBSServerURL))
		parts = append(parts, fmt.Sprintf(`--bbs-project "%s"`, g.options.BBSProject))
		parts = append(parts, fmt.Sprintf(`--bbs-repo "%s"`, repo.Name))
	} else {
		// GitHub to GitHub
		parts = append(parts, fmt.Sprintf(`--github-source-org "%s"`, g.options.SourceOrg))
		parts = append(parts, fmt.Sprintf(`--source-repo "%s"`, repo.Name))
	}

	parts = append(parts, fmt.Sprintf(`--github-target-org "%s"`, g.options.TargetOrg))
	parts = append(parts, fmt.Sprintf(`--target-repo "%s"`, repo.Name))

	// GHES options
	if g.options.GHESAPIUrl != "" {
		parts = append(parts, fmt.Sprintf(`--ghes-api-url "%s"`, g.options.GHESAPIUrl))
		if g.options.AWSBucketName != "" {
			parts = append(parts, fmt.Sprintf(`--aws-bucket-name "%s"`, g.options.AWSBucketName))
		}
		if g.options.AWSRegion != "" {
			parts = append(parts, fmt.Sprintf(`--aws-region "%s"`, g.options.AWSRegion))
		}
		if g.options.NoSSLVerify {
			parts = append(parts, "--no-ssl-verify")
		}
		if g.options.KeepArchive {
			parts = append(parts, "--keep-archive")
		}
		if g.options.UseGithubStorage {
			parts = append(parts, "--use-github-storage")
		}
	}

	if g.options.Verbose {
		parts = append(parts, "--verbose")
	}
	if !wait {
		parts = append(parts, "--queue-only")
	}
	if g.options.SkipReleases {
		parts = append(parts, "--skip-releases")
	}
	if g.options.LockSourceRepo {
		parts = append(parts, "--lock-source-repo")
	}

	parts = append(parts, fmt.Sprintf("--target-repo-visibility %s", repo.Visibility))

	return strings.Join(parts, " ")
}

// buildWaitForMigrationCommand builds the wait-for-migration command
func (g *Generator) buildWaitForMigrationCommand(repoName string) string {
	var parts []string

	parts = append(parts, g.options.CLICommand+" wait-for-migration")

	if g.options.TargetAPIURL != "" {
		parts = append(parts, fmt.Sprintf(`--target-api-url "%s"`, g.options.TargetAPIURL))
	}

	parts = append(parts, fmt.Sprintf(`--migration-id $RepoMigrations["%s"]`, repoName))

	return strings.Join(parts, " ")
}

// buildDownloadLogsCommand builds the download-logs command
func (g *Generator) buildDownloadLogsCommand(repoName string) string {
	var parts []string

	parts = append(parts, g.options.CLICommand+" download-logs")

	if g.options.TargetAPIURL != "" {
		parts = append(parts, fmt.Sprintf(`--target-api-url "%s"`, g.options.TargetAPIURL))
	}

	parts = append(parts, fmt.Sprintf(`--github-target-org "%s"`, g.options.TargetOrg))
	parts = append(parts, fmt.Sprintf(`--target-repo "%s"`, repoName))

	return strings.Join(parts, " ")
}

// versionComment returns the version comment for the script header
func (g *Generator) versionComment() string {
	return fmt.Sprintf("# =========== Created with CLI version %s ===========", g.options.CLIVersion)
}
