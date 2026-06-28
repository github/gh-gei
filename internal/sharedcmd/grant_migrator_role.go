package sharedcmd

import (
	"context"
	"strings"

	"github.com/github/gh-gei/internal/cmdutil"
	"github.com/github/gh-gei/pkg/logger"
)

// MigratorRoleGranter is the consumer-defined interface for granting migrator roles.
type MigratorRoleGranter interface {
	GetOrganizationId(ctx context.Context, org string) (string, error)
	GrantMigratorRole(ctx context.Context, orgID, actor, actorType string) (bool, error)
}

// ValidateMigratorRoleArgs validates the shared arguments for grant/revoke migrator role commands.
func ValidateMigratorRoleArgs(githubOrg, actor, actorType, ghesAPIURL, targetAPIURL string) error {
	if strings.TrimSpace(githubOrg) == "" {
		return cmdutil.NewUserError("--github-org must be provided")
	}
	if strings.TrimSpace(actor) == "" {
		return cmdutil.NewUserError("--actor must be provided")
	}
	if strings.HasPrefix(githubOrg, "http://") || strings.HasPrefix(githubOrg, "https://") {
		return cmdutil.NewUserError("The --github-org option expects an organization name, not a URL. Please provide just the organization name.")
	}

	upper := strings.ToUpper(actorType)
	if upper != "TEAM" && upper != "USER" {
		return cmdutil.NewUserError("Actor type must be either TEAM or USER.")
	}

	if ghesAPIURL != "" && targetAPIURL != "" {
		return cmdutil.NewUserError("Only one of --ghes-api-url or --target-api-url can be set at a time.")
	}

	return nil
}

func RunGrantMigratorRole(ctx context.Context, gh MigratorRoleGranter, log *logger.Logger, githubOrg, actor, actorType string) error {
	log.Info("Granting migrator role ...")

	orgID, err := gh.GetOrganizationId(ctx, githubOrg)
	if err != nil {
		return err
	}

	success, err := gh.GrantMigratorRole(ctx, orgID, actor, actorType)
	if err != nil {
		return err
	}

	if success {
		log.Success("Migrator role successfully set for the %s \"%s\"", actorType, actor)
	} else {
		log.Errorf("Migrator role couldn't be set for the %s \"%s\"", actorType, actor)
	}

	return nil
}
