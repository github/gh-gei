package main

import (
	"context"
	"os"

	"github.com/github/gh-gei/internal/sharedcmd"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/spf13/cobra"
)

// mannequinReclaimer is the consumer-defined interface for the reclaim service.
type mannequinReclaimer = sharedcmd.MannequinReclaimer

// mannequinReclaimAPI is the consumer-defined interface for direct GitHub API calls.
type mannequinReclaimAPI = sharedcmd.MannequinReclaimAPI

// newReclaimMannequinCmd creates the reclaim-mannequin cobra command.
func newReclaimMannequinCmd(
	svc mannequinReclaimer,
	api mannequinReclaimAPI,
	log *logger.Logger,
	fileExists func(string) bool,
	readFile func(string) ([]string, error),
) *cobra.Command {
	var (
		githubTargetOrg string
		csv             string
		mannequinUser   string
		mannequinID     string
		targetUser      string
		force           bool
		skipInvitation  bool
		noPrompt        bool
	)

	if fileExists == nil {
		fileExists = func(path string) bool {
			_, err := os.Stat(path)
			return err == nil
		}
	}
	if readFile == nil {
		readFile = sharedcmd.ReadFileLines
	}

	cmd := &cobra.Command{
		Use:   "reclaim-mannequin",
		Short: "Reclaims one or more mannequin users",
		Long:  "Reclaims one or more mannequin users by mapping them to real GitHub users.",
		RunE: func(cmd *cobra.Command, args []string) error {
			if err := sharedcmd.ValidateReclaimMannequinArgs(githubTargetOrg, csv, mannequinUser, targetUser); err != nil {
				return err
			}
			return sharedcmd.RunReclaimMannequin(cmd.Context(), svc, api, log, fileExists, readFile,
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
	cmd.Flags().String("github-target-pat", "", "Personal access token for the target GitHub instance")
	cmd.Flags().String("target-api-url", "", "API URL for the target GitHub instance")

	return cmd
}

// validateReclaimMannequinArgs delegates to sharedcmd for backward compat with tests.
func validateReclaimMannequinArgs(githubTargetOrg, csv, mannequinUser, targetUser string) error {
	return sharedcmd.ValidateReclaimMannequinArgs(githubTargetOrg, csv, mannequinUser, targetUser)
}

// runReclaimMannequin delegates to sharedcmd for backward compat with tests.
func runReclaimMannequin(
	ctx context.Context,
	svc mannequinReclaimer,
	api mannequinReclaimAPI,
	log *logger.Logger,
	fileExists func(string) bool,
	readFile func(string) ([]string, error),
	org, csv, mannequinUser, mannequinID, targetUser string,
	force, skipInvitation, noPrompt bool,
) error {
	return sharedcmd.RunReclaimMannequin(ctx, svc, api, log, fileExists, readFile,
		org, csv, mannequinUser, mannequinID, targetUser, force, skipInvitation, noPrompt)
}
