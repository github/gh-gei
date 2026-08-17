package sharedcmd

import (
	"bufio"
	"context"
	"os"
	"strings"

	"github.com/github/gh-gei/internal/cmdutil"
	"github.com/github/gh-gei/pkg/logger"
)

// MannequinReclaimer is the consumer-defined interface for the reclaim service.
type MannequinReclaimer interface {
	ReclaimMannequin(ctx context.Context, mannequinUser, mannequinID, targetUser, org string, force, skipInvitation bool) error
	ReclaimMannequins(ctx context.Context, lines []string, org string, force, skipInvitation bool) error
}

// MannequinReclaimAPI is the consumer-defined interface for direct GitHub API calls
// needed by the reclaim-mannequin command (skip-invitation admin check).
type MannequinReclaimAPI interface {
	GetLoginName(ctx context.Context) (string, error)
	GetOrgMembershipForUser(ctx context.Context, org, member string) (string, error)
}

func ValidateReclaimMannequinArgs(githubTargetOrg, csv, mannequinUser, targetUser string) error {
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

func RunReclaimMannequin(
	ctx context.Context,
	svc MannequinReclaimer,
	api MannequinReclaimAPI,
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

// ReadFileLines reads a file and returns its lines.
func ReadFileLines(path string) ([]string, error) {
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
