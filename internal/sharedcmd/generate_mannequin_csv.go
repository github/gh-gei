package sharedcmd

import (
	"context"
	"fmt"
	"strings"

	"github.com/github/gh-gei/internal/cmdutil"
	"github.com/github/gh-gei/pkg/github"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/github/gh-gei/pkg/mannequin"
)

// MannequinCSVGenerator is the consumer-defined interface for generate-mannequin-csv.
type MannequinCSVGenerator interface {
	GetOrganizationId(ctx context.Context, org string) (string, error)
	GetMannequins(ctx context.Context, orgID string) ([]github.Mannequin, error)
}

func ValidateGenerateMannequinCSVArgs(githubTargetOrg string) error {
	if strings.TrimSpace(githubTargetOrg) == "" {
		return cmdutil.NewUserError("--github-target-org must be provided")
	}
	if strings.HasPrefix(githubTargetOrg, "http://") || strings.HasPrefix(githubTargetOrg, "https://") {
		return cmdutil.NewUserError("The --github-target-org option expects an organization name, not a URL. Please provide just the organization name.")
	}
	return nil
}

func RunGenerateMannequinCSV(ctx context.Context, gh MannequinCSVGenerator, log *logger.Logger, writeFile func(path, content string) error, org, output string, includeReclaimed bool) error {
	log.Info("Generating CSV...")

	orgID, err := gh.GetOrganizationId(ctx, org)
	if err != nil {
		return err
	}

	mannequins, err := gh.GetMannequins(ctx, orgID)
	if err != nil {
		return err
	}

	reclaimedCount := 0
	for _, m := range mannequins {
		if m.MappedUser != nil {
			reclaimedCount++
		}
	}

	log.Info("    # Mannequins Found: %d", len(mannequins))
	log.Info("    # Mannequins Previously Reclaimed: %d", reclaimedCount)

	var sb strings.Builder
	sb.WriteString(mannequin.CSVHeader)
	sb.WriteString("\n")

	for _, m := range mannequins {
		if !includeReclaimed && m.MappedUser != nil {
			continue
		}
		mappedLogin := ""
		if m.MappedUser != nil {
			mappedLogin = m.MappedUser.Login
		}
		fmt.Fprintf(&sb, "%s,%s,%s\n", m.Login, m.ID, mappedLogin)
	}

	return writeFile(output, sb.String())
}
