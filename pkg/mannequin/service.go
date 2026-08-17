// Package mannequin provides services for reclaiming mannequin users.
package mannequin

import (
	"context"
	"fmt"
	"strings"

	"github.com/github/gh-gei/internal/cmdutil"
	"github.com/github/gh-gei/pkg/github"
	"github.com/github/gh-gei/pkg/logger"
)

// CSVHeader is the required header for mannequin CSV files.
const CSVHeader = "mannequin-user,mannequin-id,target-user"

// GitHubAPI is the consumer-defined interface for GitHub API calls needed by the reclaim service.
type GitHubAPI interface {
	GetOrganizationId(ctx context.Context, org string) (string, error)
	GetMannequins(ctx context.Context, orgID string) ([]github.Mannequin, error)
	GetMannequinsByLogin(ctx context.Context, orgID, login string) ([]github.Mannequin, error)
	GetUserId(ctx context.Context, login string) (string, error)
	CreateAttributionInvitation(ctx context.Context, orgID, sourceID, targetID string) (*github.CreateAttributionInvitationResult, error)
	ReclaimMannequinSkipInvitation(ctx context.Context, orgID, sourceID, targetID string) (*github.ReattributeMannequinToUserResult, error)
}

// ReclaimService handles reclaiming mannequin users.
type ReclaimService struct {
	gh  GitHubAPI
	log *logger.Logger
}

// NewReclaimService creates a new ReclaimService.
func NewReclaimService(gh GitHubAPI, log *logger.Logger) *ReclaimService {
	return &ReclaimService{gh: gh, log: log}
}

// ReclaimMannequin reclaims a single mannequin by login, optionally filtering by mannequin ID.
func (s *ReclaimService) ReclaimMannequin(ctx context.Context, mannequinUser, mannequinID, targetUser, org string, force, skipInvitation bool) error {
	orgID, err := s.gh.GetOrganizationId(ctx, org)
	if err != nil {
		return err
	}

	allByLogin, err := s.gh.GetMannequinsByLogin(ctx, orgID, mannequinUser)
	if err != nil {
		return err
	}

	filtered := filterByLogin(allByLogin, mannequinUser, mannequinID)

	if len(filtered) == 0 {
		return cmdutil.NewUserErrorf("User %s is not a mannequin.", mannequinUser)
	}

	if !force && isClaimed(filtered, mannequinUser, "") {
		return cmdutil.NewUserErrorf("User %s is already mapped to a user. Use the force option if you want to reclaim the mannequin again.", mannequinUser)
	}

	targetUserID, err := s.gh.GetUserId(ctx, targetUser)
	if err != nil {
		return err
	}

	if skipInvitation {
		for _, m := range uniqueUsers(filtered) {
			result, err := s.gh.ReclaimMannequinSkipInvitation(ctx, orgID, m.ID, targetUserID)
			if err != nil {
				return err
			}
			if !s.handleReclamationResult(m.Login, targetUser, m, targetUserID, result) {
				return cmdutil.NewUserError("Failed to reclaim mannequin.")
			}
		}
	} else {
		success := true
		for _, m := range uniqueUsers(filtered) {
			result, err := s.gh.CreateAttributionInvitation(ctx, orgID, m.ID, targetUserID)
			if err != nil {
				return err
			}
			success = s.handleInvitationResult(mannequinUser, targetUser, m, targetUserID, result) && success
		}
		if !success {
			return cmdutil.NewUserError("Failed to send reclaim mannequin invitation(s).")
		}
	}

	return nil
}

// ReclaimMannequins processes a CSV file of mannequins to reclaim in bulk.
func (s *ReclaimService) ReclaimMannequins(ctx context.Context, lines []string, org string, force, skipInvitation bool) error {
	if len(lines) == 0 {
		s.log.Warning("File is empty. Nothing to reclaim")
		return nil
	}

	if !strings.EqualFold(CSVHeader, lines[0]) {
		return cmdutil.NewUserErrorf("Invalid Header. Should be: %s", CSVHeader)
	}

	orgID, err := s.gh.GetOrganizationId(ctx, org)
	if err != nil {
		return err
	}

	allMannequins, err := s.gh.GetMannequins(ctx, orgID)
	if err != nil {
		return err
	}

	// Parse CSV lines (skip header, skip blank lines)
	var parsed []parsedEntry

	for _, line := range lines[1:] {
		if strings.TrimSpace(line) == "" {
			continue
		}
		login, id, claimant := s.parseLine(line)
		parsed = append(parsed, parsedEntry{login, id, claimant})
	}

	for i, entry := range parsed {
		stop, err := s.processCSVEntry(ctx, entry, i, parsed, allMannequins, orgID, force, skipInvitation)
		if err != nil {
			return err
		}
		if stop {
			return nil
		}
	}

	return nil
}

// processCSVEntry processes a single CSV entry during bulk reclaim.
// Returns (stop, error) where stop=true means processing should halt.
func (s *ReclaimService) processCSVEntry(
	ctx context.Context,
	entry parsedEntry,
	idx int,
	parsed []parsedEntry,
	allMannequins []github.Mannequin,
	orgID string,
	force, skipInvitation bool,
) (bool, error) {
	if entry.login == "" {
		return false, nil
	}

	if !force && isClaimed(allMannequins, entry.login, entry.id) {
		s.log.Warning("%s is already claimed. Skipping (use force if you want to reclaim)", entry.login)
		return false, nil
	}

	if findFirst(allMannequins, entry.login, entry.id) == nil {
		s.log.Warning("Mannequin %s not found. Skipping.", entry.login)
		return false, nil
	}

	if isDuplicate(parsed, idx) {
		s.log.Warning("Mannequin %s is a duplicate. Skipping.", entry.login)
		return false, nil
	}

	claimantID, err := s.gh.GetUserId(ctx, entry.claimantLogin)
	if err != nil {
		if strings.Contains(err.Error(), "Could not resolve to a User") {
			s.log.Warning("Claimant \"%s\" not found. Will ignore it.", entry.claimantLogin)
			return false, nil
		}
		return false, err
	}

	if skipInvitation {
		result, err := s.gh.ReclaimMannequinSkipInvitation(ctx, orgID, entry.id, claimantID)
		if err != nil {
			return false, err
		}
		if !s.handleReclamationResult(entry.login, entry.claimantLogin, github.Mannequin{ID: entry.id, Login: entry.login}, claimantID, result) {
			return true, nil // stop processing on fail-fast (EMU error)
		}
	} else {
		result, err := s.gh.CreateAttributionInvitation(ctx, orgID, entry.id, claimantID)
		if err != nil {
			return false, err
		}
		// Return value intentionally discarded in CSV bulk mode: invitation failures
		// are logged but processing continues for remaining entries (matches C# behavior).
		s.handleInvitationResult(entry.login, entry.claimantLogin, github.Mannequin{ID: entry.id, Login: entry.login}, claimantID, result)
	}

	return false, nil
}

func (s *ReclaimService) parseLine(line string) (login, id, claimantLogin string) {
	parts := strings.Split(line, ",")
	if len(parts) != 3 {
		s.log.Warning("Invalid line: \"%s\". Will ignore it.", line)
		return "", "", ""
	}

	login = strings.TrimSpace(parts[0])
	id = strings.TrimSpace(parts[1])
	claimantLogin = strings.TrimSpace(parts[2])

	if login == "" {
		s.log.Warning("Invalid line: \"%s\". Mannequin login is not defined. Will ignore it.", line)
		return "", "", ""
	}
	if id == "" {
		s.log.Warning("Invalid line: \"%s\". Mannequin Id is not defined. Will ignore it.", line)
		return "", "", ""
	}
	if claimantLogin == "" {
		s.log.Warning("Invalid line: \"%s\". Target User is not defined. Will ignore it.", line)
		return "", "", ""
	}

	return login, id, claimantLogin
}

func (s *ReclaimService) handleInvitationResult(mannequinUser, targetUser string, m github.Mannequin, targetUserID string, result *github.CreateAttributionInvitationResult) bool {
	if len(result.Errors) > 0 {
		s.log.Errorf("Failed to send reclaim invitation email to %s for mannequin %s (%s): %s", targetUser, mannequinUser, m.ID, result.Errors[0].Message)
		return false
	}

	if result.Source == nil || result.Target == nil ||
		result.Source.ID != m.ID ||
		result.Target.ID != targetUserID {
		s.log.Errorf("Failed to send reclaim invitation email to %s for mannequin %s (%s)", targetUser, mannequinUser, m.ID)
		return false
	}

	s.log.Info("Mannequin reclaim invitation email successfully sent to %s for %s (%s)", targetUser, mannequinUser, m.ID)
	return true
}

func (s *ReclaimService) handleReclamationResult(mannequinUser, targetUser string, m github.Mannequin, targetUserID string, result *github.ReattributeMannequinToUserResult) bool {
	if len(result.Errors) > 0 {
		msg := result.Errors[0].Message
		if strings.Contains(msg, "is not an Enterprise Managed Users (EMU) organization") {
			s.log.Errorf("Failed to reclaim mannequins. The --skip-invitation flag is only available to EMU organizations.")
			return false
		}
		s.log.Warning("Failed to reattribute content belonging to mannequin %s (%s) to %s: %s", mannequinUser, m.ID, targetUser, msg)
		return true
	}

	if result.Source == nil || result.Target == nil ||
		result.Source.ID != m.ID ||
		result.Target.ID != targetUserID {
		s.log.Warning("Failed to reattribute content belonging to mannequin %s (%s) to %s", mannequinUser, m.ID, targetUser)
		return true
	}

	s.log.Info("Successfully reclaimed content belonging to mannequin %s (%s) to %s", mannequinUser, m.ID, targetUser)
	return true
}

// parsedEntry represents a parsed line from the mannequin CSV.
type parsedEntry struct {
	login         string
	id            string
	claimantLogin string
}

// --- helper functions ---

func filterByLogin(mannequins []github.Mannequin, login, id string) []github.Mannequin {
	var result []github.Mannequin
	for _, m := range mannequins {
		if strings.EqualFold(m.Login, login) && (id == "" || strings.EqualFold(m.ID, id)) {
			result = append(result, m)
		}
	}
	return result
}

func isClaimed(mannequins []github.Mannequin, login, id string) bool {
	for _, m := range mannequins {
		if strings.EqualFold(m.Login, login) &&
			(id == "" || strings.EqualFold(m.ID, id)) &&
			m.MappedUser != nil {
			return true
		}
	}
	return false
}

func findFirst(mannequins []github.Mannequin, login, id string) *github.Mannequin {
	for i, m := range mannequins {
		if strings.EqualFold(m.Login, login) && strings.EqualFold(m.ID, id) {
			return &mannequins[i]
		}
	}
	return nil
}

func uniqueUsers(mannequins []github.Mannequin) []github.Mannequin {
	seen := make(map[string]bool)
	var result []github.Mannequin
	for _, m := range mannequins {
		key := fmt.Sprintf("%s__%s", m.ID, m.Login)
		if !seen[key] {
			seen[key] = true
			result = append(result, m)
		}
	}
	return result
}

func isDuplicate(parsed []parsedEntry, idx int) bool {
	target := parsed[idx]
	count := 0
	for _, p := range parsed {
		if p.login == target.login && p.id == target.id {
			count++
		}
	}
	return count > 1
}
