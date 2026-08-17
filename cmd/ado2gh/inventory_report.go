package main

import (
	"context"
	"os"

	"github.com/github/gh-gei/pkg/ado"
	"github.com/github/gh-gei/pkg/env"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/spf13/cobra"
)

// ---------------------------------------------------------------------------
// Consumer-defined interfaces
// ---------------------------------------------------------------------------

// inventoryInspector defines the inspector methods needed by inventory-report.
type inventoryInspector interface {
	ado.CSVInspector
	SetOrgFilter(string)
	GetOrgFilter() string
	GetTeamProjectCount(ctx context.Context) (int, error)
	GetRepoCount(ctx context.Context) (int, error)
	GetPipelineCount(ctx context.Context) (int, error)
}

// inventoryAPI is the ADO API interface for inventory-report.
type inventoryAPI = ado.CSVAdoAPI

// ---------------------------------------------------------------------------
// Args struct
// ---------------------------------------------------------------------------

type inventoryReportArgs struct {
	adoOrg  string
	adoPAT  string
	minimal bool
}

// ---------------------------------------------------------------------------
// Command constructor (testable)
// ---------------------------------------------------------------------------

func newInventoryReportCmd(
	ins inventoryInspector,
	api inventoryAPI,
	log *logger.Logger,
	writeFile func(string, string) error,
) *cobra.Command {
	var a inventoryReportArgs

	cmd := &cobra.Command{
		Use:   "inventory-report",
		Short: "Generates several CSV files containing lists of ADO orgs, team projects, repos, and pipelines",
		Long: "Generates several CSV files containing lists of ADO orgs, team projects, repos, and pipelines. Useful for planning large migrations.\n" +
			"Note: Expects ADO_PAT env variable or --ado-pat option to be set.",
		RunE: func(cmd *cobra.Command, _ []string) error {
			return runInventoryReport(cmd.Context(), ins, api, log, a, writeFile)
		},
	}

	cmd.Flags().StringVar(&a.adoOrg, "ado-org", "", "If not provided will iterate over all orgs that ADO_PAT has access to.")
	cmd.Flags().StringVar(&a.adoPAT, "ado-pat", "", "")
	cmd.Flags().BoolVar(&a.minimal, "minimal", false, "Significantly speeds up the generation of the CSV files by including the bare minimum info.")

	return cmd
}

// ---------------------------------------------------------------------------
// Production command constructor
// ---------------------------------------------------------------------------

func newInventoryReportCmdLive() *cobra.Command {
	var a inventoryReportArgs

	cmd := &cobra.Command{
		Use:   "inventory-report",
		Short: "Generates several CSV files containing lists of ADO orgs, team projects, repos, and pipelines",
		Long: "Generates several CSV files containing lists of ADO orgs, team projects, repos, and pipelines. Useful for planning large migrations.\n" +
			"Note: Expects ADO_PAT env variable or --ado-pat option to be set.",
		RunE: func(cmd *cobra.Command, _ []string) error {
			log := getLogger(cmd)
			envProv := env.New()

			adoPAT := a.adoPAT
			if adoPAT == "" {
				adoPAT = envProv.ADOPAT()
			}

			client := ado.NewClient("https://dev.azure.com", adoPAT, log)
			ins := ado.NewInspector(log, client)

			writeFile := func(path, content string) error {
				return os.WriteFile(path, []byte(content), 0o600)
			}

			return runInventoryReport(cmd.Context(), ins, client, log, a, writeFile)
		},
	}

	cmd.Flags().StringVar(&a.adoOrg, "ado-org", "", "If not provided will iterate over all orgs that ADO_PAT has access to.")
	cmd.Flags().StringVar(&a.adoPAT, "ado-pat", "", "")
	cmd.Flags().BoolVar(&a.minimal, "minimal", false, "Significantly speeds up the generation of the CSV files by including the bare minimum info.")

	return cmd
}

// ---------------------------------------------------------------------------
// Runner
// ---------------------------------------------------------------------------

func runInventoryReport(
	ctx context.Context,
	ins inventoryInspector,
	api inventoryAPI,
	log *logger.Logger,
	a inventoryReportArgs,
	writeFile func(string, string) error,
) error {
	log.Info("Creating inventory report...")

	if a.adoOrg != "" {
		ins.SetOrgFilter(a.adoOrg)
	}

	// Populate caches and log counts
	orgs, err := ins.GetOrgs(ctx)
	if err != nil {
		return err
	}
	log.Info("Found %d orgs", len(orgs))

	tpCount, err := ins.GetTeamProjectCount(ctx)
	if err != nil {
		return err
	}
	log.Info("Found %d team projects", tpCount)

	repoCount, err := ins.GetRepoCount(ctx)
	if err != nil {
		return err
	}
	log.Info("Found %d repos", repoCount)

	pipelineCount, err := ins.GetPipelineCount(ctx)
	if err != nil {
		return err
	}
	log.Info("Found %d pipelines", pipelineCount)

	// Generate CSVs
	orgsCsv, err := ado.GenerateOrgsCsv(ctx, ins, api, a.minimal)
	if err != nil {
		return err
	}

	tpCsv, err := ado.GenerateTeamProjectsCsv(ctx, ins, api, a.minimal)
	if err != nil {
		return err
	}

	reposCsv, err := ado.GenerateReposCsv(ctx, ins, api, a.minimal)
	if err != nil {
		return err
	}

	pipelinesCsv, err := ado.GeneratePipelinesCsv(ctx, ins, api)
	if err != nil {
		return err
	}

	// Write files
	files := map[string]string{
		"orgs.csv":          orgsCsv,
		"team-projects.csv": tpCsv,
		"repos.csv":         reposCsv,
		"pipelines.csv":     pipelinesCsv,
	}

	for name, content := range files {
		if err := writeFile(name, content); err != nil {
			return err
		}
		log.Info("Wrote %s", name)
	}

	return nil
}
