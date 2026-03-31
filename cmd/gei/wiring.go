package main

// wiring.go contains "live" constructors that wire real dependencies
// for commands that don't have their own *CmdLive() function yet.

import (
	"time"

	"github.com/github/gh-gei/pkg/download"
	"github.com/github/gh-gei/pkg/env"
	"github.com/github/gh-gei/pkg/filesystem"
	"github.com/github/gh-gei/pkg/github"
	"github.com/github/gh-gei/pkg/mannequin"
	"github.com/spf13/cobra"
)

// resolveSimpleTargetPAT resolves a target PAT from a flag value or the GH_PAT env var.
// This is the simple version used by commands that only need a target token
// (as opposed to resolveTargetToken in migrate_repo.go which uses the migrateRepoEnvProvider interface).
func resolveSimpleTargetPAT(flagValue string, envProv *env.Provider) string {
	if flagValue != "" {
		return flagValue
	}
	return envProv.TargetGitHubPAT()
}

// resolveSimpleTargetAPIURL returns the target API URL, defaulting to api.github.com.
func resolveSimpleTargetAPIURL(flagValue string) string {
	if flagValue != "" {
		return flagValue
	}
	return defaultGitHubAPIURL
}

// newWaitForMigrationCmdLive wires real dependencies for wait-for-migration.
func newWaitForMigrationCmdLive() *cobra.Command {
	var (
		migrationID     string
		githubTargetPAT string
		targetAPIURL    string
	)

	cmd := &cobra.Command{
		Use:   "wait-for-migration",
		Short: "Waits for a migration to finish",
		Long:  "Polls the migration status API until a repository or organization migration completes or fails.",
		RunE: func(cmd *cobra.Command, _ []string) error {
			log := getLogger(cmd)
			envProv := env.New()

			token := resolveSimpleTargetPAT(githubTargetPAT, envProv)
			apiURL := resolveSimpleTargetAPIURL(targetAPIURL)

			gh := github.NewClient(token,
				github.WithAPIURL(apiURL),
				github.WithLogger(log),
				github.WithVersion(version),
			)

			if err := validateMigrationID(migrationID); err != nil {
				return err
			}
			return runWaitForMigration(cmd.Context(), gh, log, migrationID, defaultPollInterval)
		},
	}

	cmd.Flags().StringVar(&migrationID, "migration-id", "", "The ID of the migration to wait for (REQUIRED)")
	cmd.Flags().StringVar(&githubTargetPAT, "github-target-pat", "", "Personal access token for the target GitHub instance")
	cmd.Flags().StringVar(&targetAPIURL, "target-api-url", "", "API URL for the target GitHub instance")

	return cmd
}

// newAbortMigrationCmdLive wires real dependencies for abort-migration.
func newAbortMigrationCmdLive() *cobra.Command {
	var (
		migrationID     string
		githubTargetPAT string
		targetAPIURL    string
	)

	cmd := &cobra.Command{
		Use:   "abort-migration",
		Short: "Aborts a repository migration that is queued or in progress",
		Long:  "Aborts a repository migration that is queued or in progress.",
		RunE: func(cmd *cobra.Command, _ []string) error {
			log := getLogger(cmd)
			envProv := env.New()

			token := resolveSimpleTargetPAT(githubTargetPAT, envProv)
			apiURL := resolveSimpleTargetAPIURL(targetAPIURL)

			gh := github.NewClient(token,
				github.WithAPIURL(apiURL),
				github.WithLogger(log),
				github.WithVersion(version),
			)

			if err := validateAbortMigrationID(migrationID); err != nil {
				return err
			}
			return runAbortMigration(cmd.Context(), gh, log, migrationID)
		},
	}

	cmd.Flags().StringVar(&migrationID, "migration-id", "",
		"The ID of the migration to abort, starting with RM_. Organization migrations, where the ID starts with OM_, are not supported.")
	cmd.Flags().StringVar(&githubTargetPAT, "github-target-pat", "", "Personal access token for the target GitHub instance")
	cmd.Flags().StringVar(&targetAPIURL, "target-api-url", "", "API URL for the target GitHub instance")

	return cmd
}

// newDownloadLogsCmdLive wires real dependencies for download-logs.
func newDownloadLogsCmdLive() *cobra.Command {
	var (
		migrationID     string
		githubTargetOrg string
		targetRepo      string
		logFile         string
		overwrite       bool
		githubTargetPAT string
		targetAPIURL    string
	)

	cmd := &cobra.Command{
		Use:   "download-logs",
		Short: "Downloads migration logs for a repository migration",
		Long:  "Downloads migration logs for a repository migration, either by migration ID or by org/repo.",
		RunE: func(cmd *cobra.Command, _ []string) error {
			log := getLogger(cmd)
			envProv := env.New()

			token := resolveSimpleTargetPAT(githubTargetPAT, envProv)
			apiURL := resolveSimpleTargetAPIURL(targetAPIURL)

			gh := github.NewClient(token,
				github.WithAPIURL(apiURL),
				github.WithLogger(log),
				github.WithVersion(version),
			)

			dl := download.New(nil)
			fc := filesystem.New()

			opts := downloadLogsOptions{
				maxRetries: 10,
				retryDelay: 5 * time.Second,
			}

			return runDownloadLogs(cmd.Context(), gh, dl, fc, log, downloadLogsParams{
				migrationID:     migrationID,
				githubTargetOrg: githubTargetOrg,
				targetRepo:      targetRepo,
				logFile:         logFile,
				overwrite:       overwrite,
				maxRetries:      opts.maxRetries,
				retryDelay:      opts.retryDelay,
			})
		},
	}

	cmd.Flags().StringVar(&migrationID, "migration-id", "", "The ID of the migration")
	cmd.Flags().StringVar(&githubTargetOrg, "github-target-org", "", "Target GitHub organization")
	cmd.Flags().StringVar(&targetRepo, "target-repo", "", "Target repository name")
	cmd.Flags().StringVar(&logFile, "migration-log-file", "", "Custom output filename for the migration log")
	cmd.Flags().BoolVar(&overwrite, "overwrite", false, "Overwrite the log file if it already exists")
	cmd.Flags().StringVar(&githubTargetPAT, "github-target-pat", "", "Personal access token for the target GitHub instance")
	cmd.Flags().StringVar(&targetAPIURL, "target-api-url", "", "API URL for the target GitHub instance")

	return cmd
}

// newGrantMigratorRoleCmdLive wires real dependencies for grant-migrator-role.
func newGrantMigratorRoleCmdLive() *cobra.Command {
	var (
		githubOrg       string
		actor           string
		actorType       string
		githubTargetPAT string
		targetAPIURL    string
		ghesAPIURL      string
	)

	cmd := &cobra.Command{
		Use:   "grant-migrator-role",
		Short: "Grants the migrator role to a user or team for a GitHub organization",
		Long:  "Grants the migrator role to a user or team for a GitHub organization.",
		RunE: func(cmd *cobra.Command, _ []string) error {
			log := getLogger(cmd)
			envProv := env.New()

			token := resolveSimpleTargetPAT(githubTargetPAT, envProv)
			apiURL := resolveSimpleTargetAPIURL(targetAPIURL)
			if ghesAPIURL != "" {
				apiURL = ghesAPIURL
			}

			gh := github.NewClient(token,
				github.WithAPIURL(apiURL),
				github.WithLogger(log),
				github.WithVersion(version),
			)

			if err := validateMigratorRoleArgs(githubOrg, actor, actorType, cmd); err != nil {
				return err
			}
			return runGrantMigratorRole(cmd.Context(), gh, log, githubOrg, actor, actorType)
		},
	}

	cmd.Flags().StringVar(&githubOrg, "github-org", "", "The GitHub organization to grant the migrator role for (REQUIRED)")
	cmd.Flags().StringVar(&actor, "actor", "", "The user or team to grant the migrator role to (REQUIRED)")
	cmd.Flags().StringVar(&actorType, "actor-type", "", "The type of the actor (USER or TEAM) (REQUIRED)")
	cmd.Flags().StringVar(&githubTargetPAT, "github-target-pat", "", "Personal access token for the target GitHub instance")
	cmd.Flags().StringVar(&targetAPIURL, "target-api-url", "", "API URL for the target GitHub instance")
	cmd.Flags().StringVar(&ghesAPIURL, "ghes-api-url", "", "API URL for the source GHES instance")

	return cmd
}

// newRevokeMigratorRoleCmdLive wires real dependencies for revoke-migrator-role.
func newRevokeMigratorRoleCmdLive() *cobra.Command {
	var (
		githubOrg       string
		actor           string
		actorType       string
		githubTargetPAT string
		targetAPIURL    string
		ghesAPIURL      string
	)

	cmd := &cobra.Command{
		Use:   "revoke-migrator-role",
		Short: "Revokes the migrator role from a user or team for a GitHub organization",
		Long:  "Revokes the migrator role from a user or team for a GitHub organization.",
		RunE: func(cmd *cobra.Command, _ []string) error {
			log := getLogger(cmd)
			envProv := env.New()

			token := resolveSimpleTargetPAT(githubTargetPAT, envProv)
			apiURL := resolveSimpleTargetAPIURL(targetAPIURL)
			if ghesAPIURL != "" {
				apiURL = ghesAPIURL
			}

			gh := github.NewClient(token,
				github.WithAPIURL(apiURL),
				github.WithLogger(log),
				github.WithVersion(version),
			)

			if err := validateMigratorRoleArgs(githubOrg, actor, actorType, cmd); err != nil {
				return err
			}
			return runRevokeMigratorRole(cmd.Context(), gh, log, githubOrg, actor, actorType)
		},
	}

	cmd.Flags().StringVar(&githubOrg, "github-org", "", "The GitHub organization to revoke the migrator role for (REQUIRED)")
	cmd.Flags().StringVar(&actor, "actor", "", "The user or team to revoke the migrator role from (REQUIRED)")
	cmd.Flags().StringVar(&actorType, "actor-type", "", "The type of the actor (USER or TEAM) (REQUIRED)")
	cmd.Flags().StringVar(&githubTargetPAT, "github-target-pat", "", "Personal access token for the target GitHub instance")
	cmd.Flags().StringVar(&targetAPIURL, "target-api-url", "", "API URL for the target GitHub instance")
	cmd.Flags().StringVar(&ghesAPIURL, "ghes-api-url", "", "API URL for the source GHES instance")

	return cmd
}

// newCreateTeamCmdLive wires real dependencies for create-team.
func newCreateTeamCmdLive() *cobra.Command {
	var (
		githubOrg       string
		teamName        string
		idpGroup        string
		githubTargetPAT string
		targetAPIURL    string
	)

	cmd := &cobra.Command{
		Use:   "create-team",
		Short: "Creates a GitHub team and optionally links it to an IdP group",
		Long:  "Creates a GitHub team and optionally links it to an IdP group.",
		RunE: func(cmd *cobra.Command, _ []string) error {
			log := getLogger(cmd)
			envProv := env.New()

			token := resolveSimpleTargetPAT(githubTargetPAT, envProv)
			apiURL := resolveSimpleTargetAPIURL(targetAPIURL)

			gh := github.NewClient(token,
				github.WithAPIURL(apiURL),
				github.WithLogger(log),
				github.WithVersion(version),
			)

			if err := validateCreateTeamArgs(githubOrg, teamName); err != nil {
				return err
			}
			return runCreateTeam(cmd.Context(), gh, log, githubOrg, teamName, idpGroup)
		},
	}

	cmd.Flags().StringVar(&githubOrg, "github-org", "", "The GitHub organization to create the team in (REQUIRED)")
	cmd.Flags().StringVar(&teamName, "team-name", "", "The name of the team to create (REQUIRED)")
	cmd.Flags().StringVar(&idpGroup, "idp-group", "", "The name of the IdP group to link to the team")
	cmd.Flags().StringVar(&githubTargetPAT, "github-target-pat", "", "Personal access token for the target GitHub instance")
	cmd.Flags().StringVar(&targetAPIURL, "target-api-url", "", "API URL for the target GitHub instance")

	return cmd
}

// newGenerateMannequinCSVCmdLive wires real dependencies for generate-mannequin-csv.
func newGenerateMannequinCSVCmdLive() *cobra.Command {
	var (
		githubTargetOrg  string
		output           string
		includeReclaimed bool
		githubTargetPAT  string
		targetAPIURL     string
	)

	cmd := &cobra.Command{
		Use:   "generate-mannequin-csv",
		Short: "Generates a CSV file with mannequin users",
		Long:  "Generates a CSV file with mannequin users for an organization.",
		RunE: func(cmd *cobra.Command, _ []string) error {
			log := getLogger(cmd)
			envProv := env.New()

			token := resolveSimpleTargetPAT(githubTargetPAT, envProv)
			apiURL := resolveSimpleTargetAPIURL(targetAPIURL)

			gh := github.NewClient(token,
				github.WithAPIURL(apiURL),
				github.WithLogger(log),
				github.WithVersion(version),
			)

			if err := validateGenerateMannequinCSVArgs(githubTargetOrg); err != nil {
				return err
			}
			return runGenerateMannequinCSV(cmd.Context(), gh, log, nil, githubTargetOrg, output, includeReclaimed)
		},
	}

	cmd.Flags().StringVar(&githubTargetOrg, "github-target-org", "", "The target GitHub organization (REQUIRED)")
	cmd.Flags().StringVar(&output, "output", "mannequins.csv", "Output file path")
	cmd.Flags().BoolVar(&includeReclaimed, "include-reclaimed", false, "Include mannequins that have already been reclaimed")
	cmd.Flags().StringVar(&githubTargetPAT, "github-target-pat", "", "Personal access token for the target GitHub instance")
	cmd.Flags().StringVar(&targetAPIURL, "target-api-url", "", "API URL for the target GitHub instance")

	return cmd
}

// newReclaimMannequinCmdLive wires real dependencies for reclaim-mannequin.
func newReclaimMannequinCmdLive() *cobra.Command {
	var (
		githubTargetOrg string
		csv             string
		mannequinUser   string
		mannequinID     string
		targetUser      string
		force           bool
		skipInvitation  bool
		noPrompt        bool
		githubTargetPAT string
		targetAPIURL    string
	)

	cmd := &cobra.Command{
		Use:   "reclaim-mannequin",
		Short: "Reclaims one or more mannequin users",
		Long:  "Reclaims one or more mannequin users by mapping them to real GitHub users.",
		RunE: func(cmd *cobra.Command, _ []string) error {
			log := getLogger(cmd)
			envProv := env.New()

			token := resolveSimpleTargetPAT(githubTargetPAT, envProv)
			apiURL := resolveSimpleTargetAPIURL(targetAPIURL)

			gh := github.NewClient(token,
				github.WithAPIURL(apiURL),
				github.WithLogger(log),
				github.WithVersion(version),
			)

			svc := mannequin.NewReclaimService(gh, log)

			if err := validateReclaimMannequinArgs(githubTargetOrg, csv, mannequinUser, targetUser); err != nil {
				return err
			}
			return runReclaimMannequin(cmd.Context(), svc, gh, log, nil, nil,
				githubTargetOrg, csv, mannequinUser, mannequinID, targetUser, force, skipInvitation, noPrompt)
		},
	}

	cmd.Flags().StringVar(&githubTargetOrg, "github-target-org", "", "The target GitHub organization (REQUIRED)")
	cmd.Flags().StringVar(&csv, "csv", "", "Path to a CSV file with mannequin mappings")
	cmd.Flags().StringVar(&mannequinUser, "mannequin-user", "", "The login of the mannequin user to reclaim")
	cmd.Flags().StringVar(&mannequinID, "mannequin-id", "", "The ID of the mannequin user to reclaim")
	cmd.Flags().StringVar(&targetUser, "target-user", "", "The login of the target user to map the mannequin to")
	cmd.Flags().BoolVar(&force, "force", false, "Reclaim even if the mannequin is already mapped")
	cmd.Flags().BoolVar(&skipInvitation, "skip-invitation", false, "Skip sending an invitation email (EMU orgs only)")
	cmd.Flags().BoolVar(&noPrompt, "no-prompt", false, "Skip confirmation prompt for skip-invitation")
	cmd.Flags().StringVar(&githubTargetPAT, "github-target-pat", "", "Personal access token for the target GitHub instance")
	cmd.Flags().StringVar(&targetAPIURL, "target-api-url", "", "API URL for the target GitHub instance")

	return cmd
}
