package main

import (
	"context"
	"os"

	"github.com/github/gh-gei/internal/sharedcmd"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/spf13/cobra"
)

// mannequinCSVGenerator is the consumer-defined interface for generate-mannequin-csv.
type mannequinCSVGenerator = sharedcmd.MannequinCSVGenerator

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
			if err := sharedcmd.ValidateGenerateMannequinCSVArgs(githubTargetOrg); err != nil {
				return err
			}
			return sharedcmd.RunGenerateMannequinCSV(cmd.Context(), gh, log, writeFile, githubTargetOrg, output, includeReclaimed)
		},
	}

	cmd.Flags().StringVar(&githubTargetOrg, "github-target-org", "", "The target GitHub organization (REQUIRED)")
	cmd.Flags().StringVar(&output, "output", "mannequins.csv", "Output file path")
	cmd.Flags().BoolVar(&includeReclaimed, "include-reclaimed", false, "Include mannequins that have already been reclaimed")
	cmd.Flags().String("github-target-pat", "", "Personal access token for the target GitHub instance")
	cmd.Flags().String("target-api-url", "", "API URL for the target GitHub instance")

	return cmd
}

// validateGenerateMannequinCSVArgs delegates to sharedcmd for backward compat with tests.
func validateGenerateMannequinCSVArgs(githubTargetOrg string) error {
	return sharedcmd.ValidateGenerateMannequinCSVArgs(githubTargetOrg)
}

// runGenerateMannequinCSV delegates to sharedcmd for backward compat with tests.
func runGenerateMannequinCSV(ctx context.Context, gh mannequinCSVGenerator, log *logger.Logger, writeFile func(path, content string) error, org, output string, includeReclaimed bool) error {
	return sharedcmd.RunGenerateMannequinCSV(ctx, gh, log, writeFile, org, output, includeReclaimed)
}
