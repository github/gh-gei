package main

import (
	"bufio"
	"context"
	"os"
	"strings"

	"github.com/github/gh-gei/internal/cmdutil"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/spf13/cobra"
)

// mannequinReclaimer is the consumer-defined interface for the reclaim service.
type mannequinReclaimer interface {
	ReclaimMannequin(ctx context.Context, mannequinUser, mannequinID, targetUser, org string, force, skipInvitation bool) error
	ReclaimMannequins(ctx context.Context, lines []string, org string, force, skipInvitation bool) error
}

// mannequinReclaimAPI is the consumer-defined interface for direct GitHub API calls
// needed by the reclaim-mannequin command (skip-invitation admin check).
type mannequinReclaimAPI interface {
	GetLoginName(ctx context.Context) (string, error)
	GetOrgMembershipForUser(ctx context.Context, org, member string) (string, error)
}

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
		readFile = readFileLines
	}

	cmd := &cobra.Command{
		Use:   "reclaim-mannequin",
		Short: "Reclaims one or more mannequin users",
		Long:  "Reclaims one or more mannequin users by mapping them to real GitHub users.",
		RunE: func(cmd *cobra.Command, args []string) error {
			if err := validateReclaimMannequinArgs(githubTargetOrg, csv, mannequinUser, targetUser); err != nil {
				return err
			}
			return runReclaimMannequin(cmd.Context(), svc, api, log, fileExists, readFile,
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

func validateReclaimMannequinArgs(githubTargetOrg, csv, mannequinUser, targetUser string) error {
	if strings.TrimSpace(githubTargetOrg) == "" {
		return cmdutil.NewUserError("--github-target-org must be provided")
	}
	if strings.HasPrefix(githubTargetOrg, "http://") || strings.HasPrefix(githubTargetOrg, "https://") {
		return cmdutil.NewUserError("The --github-target-org option expects an organization name, not a URL. Please provide just the organization name.")
	}
	if csv == "" && (mannequinUser == "" || targetUser == "") {
		return cmdutil.NewUserError("Either --csv or --mannequin-user and --target-user must be specified")
	}
	return nil
}

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
	if skipInvitation {
		if !noPrompt {
			return cmdutil.NewUserError("Reclaiming mannequins with --skip-invitation is immediate and irreversible. Use --no-prompt to confirm.")
		}

		login, err := api.GetLoginName(ctx)
		if err != nil {
			return err
		}

		membership, err := api.GetOrgMembershipForUser(ctx, org, login)
		if err != nil {
			return err
		}

		if membership != "admin" {
			return cmdutil.NewUserErrorf("User %s is not an org admin and is not eligible to reclaim mannequins with the --skip-invitation feature.", login)
		}
	}

	if csv != "" {
		log.Info("Reclaiming Mannequins with CSV...")

		if !fileExists(csv) {
			return cmdutil.NewUserErrorf("File %s does not exist.", csv)
		}

		lines, err := readFile(csv)
		if err != nil {
			return err
		}

		return svc.ReclaimMannequins(ctx, lines, org, force, skipInvitation)
	}

	log.Info("Reclaiming Mannequin...")
	return svc.ReclaimMannequin(ctx, mannequinUser, mannequinID, targetUser, org, force, skipInvitation)
}

// readFileLines reads a file and returns its lines.
func readFileLines(path string) ([]string, error) {
	f, err := os.Open(path)
	if err != nil {
		return nil, err
	}
	defer f.Close()

	var lines []string
	scanner := bufio.NewScanner(f)
	for scanner.Scan() {
		lines = append(lines, scanner.Text())
	}
	return lines, scanner.Err()
}
