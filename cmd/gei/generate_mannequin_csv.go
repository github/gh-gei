package main

import (
	"context"
	"fmt"
	"os"
	"strings"

	"github.com/github/gh-gei/internal/cmdutil"
	"github.com/github/gh-gei/pkg/github"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/github/gh-gei/pkg/mannequin"
	"github.com/spf13/cobra"
)

// mannequinCSVGenerator is the consumer-defined interface for generate-mannequin-csv.
type mannequinCSVGenerator interface {
	GetOrganizationId(ctx context.Context, org string) (string, error)
	GetMannequins(ctx context.Context, orgID string) ([]github.Mannequin, error)
}

// newGenerateMannequinCSVCmd creates the generate-mannequin-csv cobra command.
func newGenerateMannequinCSVCmd(gh mannequinCSVGenerator, log *logger.Logger, writeFile func(path, content string) error) *cobra.Command {
	var (
		githubTargetOrg  string
		output           string
		includeReclaimed bool
	)

	if writeFile == nil {
		writeFile = func(path, content string) error {
			return os.WriteFile(path, []byte(content), 0o600)
		}
	}

	cmd := &cobra.Command{
		Use:   "generate-mannequin-csv",
		Short: "Generates a CSV file with mannequin users",
		Long:  "Generates a CSV file with mannequin users for an organization.",
		RunE: func(cmd *cobra.Command, args []string) error {
			if err := validateGenerateMannequinCSVArgs(githubTargetOrg); err != nil {
				return err
			}
			return runGenerateMannequinCSV(cmd.Context(), gh, log, writeFile, githubTargetOrg, output, includeReclaimed)
		},
	}

	cmd.Flags().StringVar(&githubTargetOrg, "github-target-org", "", "The target GitHub organization (REQUIRED)")
	cmd.Flags().StringVar(&output, "output", "mannequins.csv", "Output file path")
	cmd.Flags().BoolVar(&includeReclaimed, "include-reclaimed", false, "Include mannequins that have already been reclaimed")
	cmd.Flags().String("github-target-pat", "", "Personal access token for the target GitHub instance")
	cmd.Flags().String("target-api-url", "", "API URL for the target GitHub instance")

	return cmd
}

func validateGenerateMannequinCSVArgs(githubTargetOrg string) error {
	if strings.TrimSpace(githubTargetOrg) == "" {
		return cmdutil.NewUserError("--github-target-org must be provided")
	}
	if strings.HasPrefix(githubTargetOrg, "http://") || strings.HasPrefix(githubTargetOrg, "https://") {
		return cmdutil.NewUserError("The --github-target-org option expects an organization name, not a URL. Please provide just the organization name.")
	}
	return nil
}

func runGenerateMannequinCSV(ctx context.Context, gh mannequinCSVGenerator, log *logger.Logger, writeFile func(path, content string) error, org, output string, includeReclaimed bool) error {
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
