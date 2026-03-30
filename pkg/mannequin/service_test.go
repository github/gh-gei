package mannequin_test

import (
	"bytes"
	"context"
	"fmt"
	"testing"

	"github.com/github/gh-gei/pkg/github"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/github/gh-gei/pkg/mannequin"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

// mockGitHubAPI implements mannequin.GitHubAPI for testing.
type mockGitHubAPI struct {
	orgID    string
	orgIDErr error

	mannequins    []github.Mannequin
	mannequinsErr error

	mannequinsByLogin    []github.Mannequin
	mannequinsByLoginErr error

	userID    string
	userIDErr error

	invitationResult *github.CreateAttributionInvitationResult
	invitationErr    error

	reclaimResult *github.ReattributeMannequinToUserResult
	reclaimErr    error

	// capture calls
	gotOrgIDOrg          string
	gotMannequinsOrgID   string
	gotMannequinsByLogin struct{ OrgID, Login string }
	gotUserIDLogin       string
	invitations          []struct{ OrgID, SourceID, TargetID string }
	reclaims             []struct{ OrgID, SourceID, TargetID string }
}

func (m *mockGitHubAPI) GetOrganizationId(_ context.Context, org string) (string, error) {
	m.gotOrgIDOrg = org
	return m.orgID, m.orgIDErr
}

func (m *mockGitHubAPI) GetMannequins(_ context.Context, orgID string) ([]github.Mannequin, error) {
	m.gotMannequinsOrgID = orgID
	return m.mannequins, m.mannequinsErr
}

func (m *mockGitHubAPI) GetMannequinsByLogin(_ context.Context, orgID, login string) ([]github.Mannequin, error) {
	m.gotMannequinsByLogin = struct{ OrgID, Login string }{orgID, login}
	return m.mannequinsByLogin, m.mannequinsByLoginErr
}

func (m *mockGitHubAPI) GetUserId(_ context.Context, login string) (string, error) {
	m.gotUserIDLogin = login
	return m.userID, m.userIDErr
}

func (m *mockGitHubAPI) CreateAttributionInvitation(_ context.Context, orgID, sourceID, targetID string) (*github.CreateAttributionInvitationResult, error) {
	m.invitations = append(m.invitations, struct{ OrgID, SourceID, TargetID string }{orgID, sourceID, targetID})
	return m.invitationResult, m.invitationErr
}

func (m *mockGitHubAPI) ReclaimMannequinSkipInvitation(_ context.Context, orgID, sourceID, targetID string) (*github.ReattributeMannequinToUserResult, error) {
	m.reclaims = append(m.reclaims, struct{ OrgID, SourceID, TargetID string }{orgID, sourceID, targetID})
	return m.reclaimResult, m.reclaimErr
}

const (
	testOrg   = "FooOrg"
	testOrgID = "org-id-123"
)

// ---------- ParseLine tests (tested indirectly through ReclaimMannequins) ----------

func TestReclaimMannequins_InvalidLine_Skipped(t *testing.T) {
	mock := &mockGitHubAPI{
		orgID: testOrgID,
		mannequins: []github.Mannequin{
			{ID: "m1", Login: "mona"},
		},
	}
	var buf bytes.Buffer
	log := logger.New(false, &buf)
	svc := mannequin.NewReclaimService(mock, log)

	lines := []string{
		mannequin.CSVHeader,
		"bad-line",
	}
	err := svc.ReclaimMannequins(context.Background(), lines, testOrg, false, false)
	require.NoError(t, err)
	assert.Contains(t, buf.String(), "Invalid line")
}

func TestReclaimMannequins_EmptyFieldsLine_Skipped(t *testing.T) {
	mock := &mockGitHubAPI{
		orgID: testOrgID,
		mannequins: []github.Mannequin{
			{ID: "m1", Login: "mona"},
		},
	}
	var buf bytes.Buffer
	log := logger.New(false, &buf)
	svc := mannequin.NewReclaimService(mock, log)

	lines := []string{
		mannequin.CSVHeader,
		",,",
	}
	err := svc.ReclaimMannequins(context.Background(), lines, testOrg, false, false)
	require.NoError(t, err)
	assert.Contains(t, buf.String(), "Mannequin login is not defined")
}

func TestReclaimMannequins_EmptyFile_WarnsAndReturns(t *testing.T) {
	mock := &mockGitHubAPI{}
	var buf bytes.Buffer
	log := logger.New(false, &buf)
	svc := mannequin.NewReclaimService(mock, log)

	err := svc.ReclaimMannequins(context.Background(), []string{}, testOrg, false, false)
	require.NoError(t, err)
	assert.Contains(t, buf.String(), "File is empty. Nothing to reclaim")
}

func TestReclaimMannequins_InvalidHeader_ReturnsError(t *testing.T) {
	mock := &mockGitHubAPI{}
	var buf bytes.Buffer
	log := logger.New(false, &buf)
	svc := mannequin.NewReclaimService(mock, log)

	lines := []string{"bad-header"}
	err := svc.ReclaimMannequins(context.Background(), lines, testOrg, false, false)
	require.Error(t, err)
	assert.Contains(t, err.Error(), "Invalid Header")
}

func TestReclaimMannequins_BlankLines_Skipped(t *testing.T) {
	mock := &mockGitHubAPI{
		orgID:      testOrgID,
		mannequins: []github.Mannequin{},
	}
	var buf bytes.Buffer
	log := logger.New(false, &buf)
	svc := mannequin.NewReclaimService(mock, log)

	lines := []string{
		mannequin.CSVHeader,
		"",
		"   ",
	}
	err := svc.ReclaimMannequins(context.Background(), lines, testOrg, false, false)
	require.NoError(t, err)
	// No warnings about invalid lines — blank lines are silently skipped
}

func TestReclaimMannequins_AlreadyClaimed_NotForce_Skips(t *testing.T) {
	mock := &mockGitHubAPI{
		orgID: testOrgID,
		mannequins: []github.Mannequin{
			{ID: "m1", Login: "mona", MappedUser: &github.MannequinUser{ID: "u1", Login: "mona_gh"}},
		},
	}
	var buf bytes.Buffer
	log := logger.New(false, &buf)
	svc := mannequin.NewReclaimService(mock, log)

	lines := []string{
		mannequin.CSVHeader,
		"mona,m1,target_user",
	}
	err := svc.ReclaimMannequins(context.Background(), lines, testOrg, false, false)
	require.NoError(t, err)
	assert.Contains(t, buf.String(), "already claimed")
}

func TestReclaimMannequins_MannequinNotFound_Skips(t *testing.T) {
	mock := &mockGitHubAPI{
		orgID:      testOrgID,
		mannequins: []github.Mannequin{},
	}
	var buf bytes.Buffer
	log := logger.New(false, &buf)
	svc := mannequin.NewReclaimService(mock, log)

	lines := []string{
		mannequin.CSVHeader,
		"mona,m1,target_user",
	}
	err := svc.ReclaimMannequins(context.Background(), lines, testOrg, false, false)
	require.NoError(t, err)
	assert.Contains(t, buf.String(), "not found")
}

func TestReclaimMannequins_DuplicateInCSV_Skips(t *testing.T) {
	mock := &mockGitHubAPI{
		orgID: testOrgID,
		mannequins: []github.Mannequin{
			{ID: "m1", Login: "mona"},
		},
		userID: "target-id",
		invitationResult: &github.CreateAttributionInvitationResult{
			Source: &github.Mannequin{ID: "m1", Login: "mona"},
			Target: &github.MannequinUser{ID: "target-id", Login: "target_user"},
		},
	}
	var buf bytes.Buffer
	log := logger.New(false, &buf)
	svc := mannequin.NewReclaimService(mock, log)

	lines := []string{
		mannequin.CSVHeader,
		"mona,m1,target_user",
		"mona,m1,target_user",
	}
	err := svc.ReclaimMannequins(context.Background(), lines, testOrg, false, false)
	require.NoError(t, err)
	assert.Contains(t, buf.String(), "duplicate")
}

func TestReclaimMannequins_ClaimantNotFound_Skips(t *testing.T) {
	mock := &mockGitHubAPI{
		orgID: testOrgID,
		mannequins: []github.Mannequin{
			{ID: "m1", Login: "mona"},
		},
		userIDErr: fmt.Errorf("Could not resolve to a User with the login"),
	}
	var buf bytes.Buffer
	log := logger.New(false, &buf)
	svc := mannequin.NewReclaimService(mock, log)

	lines := []string{
		mannequin.CSVHeader,
		"mona,m1,nonexistent_user",
	}
	err := svc.ReclaimMannequins(context.Background(), lines, testOrg, false, false)
	require.NoError(t, err)
	assert.Contains(t, buf.String(), "Claimant")
	assert.Contains(t, buf.String(), "not found")
}

func TestReclaimMannequins_HappyPath_Invitation(t *testing.T) {
	mock := &mockGitHubAPI{
		orgID: testOrgID,
		mannequins: []github.Mannequin{
			{ID: "m1", Login: "mona"},
		},
		userID: "target-id",
		invitationResult: &github.CreateAttributionInvitationResult{
			Source: &github.Mannequin{ID: "m1", Login: "mona"},
			Target: &github.MannequinUser{ID: "target-id", Login: "target_user"},
		},
	}
	var buf bytes.Buffer
	log := logger.New(false, &buf)
	svc := mannequin.NewReclaimService(mock, log)

	lines := []string{
		mannequin.CSVHeader,
		"mona,m1,target_user",
	}
	err := svc.ReclaimMannequins(context.Background(), lines, testOrg, false, false)
	require.NoError(t, err)
	assert.Contains(t, buf.String(), "invitation email successfully sent")
	require.Len(t, mock.invitations, 1)
	assert.Equal(t, testOrgID, mock.invitations[0].OrgID)
	assert.Equal(t, "m1", mock.invitations[0].SourceID)
	assert.Equal(t, "target-id", mock.invitations[0].TargetID)
}

func TestReclaimMannequins_HappyPath_SkipInvitation(t *testing.T) {
	mock := &mockGitHubAPI{
		orgID: testOrgID,
		mannequins: []github.Mannequin{
			{ID: "m1", Login: "mona"},
		},
		userID: "target-id",
		reclaimResult: &github.ReattributeMannequinToUserResult{
			Source: &github.Mannequin{ID: "m1", Login: "mona"},
			Target: &github.MannequinUser{ID: "target-id", Login: "target_user"},
		},
	}
	var buf bytes.Buffer
	log := logger.New(false, &buf)
	svc := mannequin.NewReclaimService(mock, log)

	lines := []string{
		mannequin.CSVHeader,
		"mona,m1,target_user",
	}
	err := svc.ReclaimMannequins(context.Background(), lines, testOrg, false, true)
	require.NoError(t, err)
	assert.Contains(t, buf.String(), "Successfully reclaimed")
	require.Len(t, mock.reclaims, 1)
}

func TestReclaimMannequins_SkipInvitation_EMUError_StopsProcessing(t *testing.T) {
	mock := &mockGitHubAPI{
		orgID: testOrgID,
		mannequins: []github.Mannequin{
			{ID: "m1", Login: "mona"},
			{ID: "m2", Login: "lisa"},
		},
		userID: "target-id",
		reclaimResult: &github.ReattributeMannequinToUserResult{
			Errors: []github.ErrorData{
				{Message: "is not an Enterprise Managed Users (EMU) organization"},
			},
		},
	}
	var buf bytes.Buffer
	log := logger.New(false, &buf)
	svc := mannequin.NewReclaimService(mock, log)

	lines := []string{
		mannequin.CSVHeader,
		"mona,m1,target_user",
		"lisa,m2,target_user",
	}
	err := svc.ReclaimMannequins(context.Background(), lines, testOrg, false, true)
	require.NoError(t, err)
	assert.Contains(t, buf.String(), "EMU organizations")
	// Should only have tried one reclaim (stopped after first)
	assert.Len(t, mock.reclaims, 1)
}

func TestReclaimMannequins_AlreadyClaimed_Force_Proceeds(t *testing.T) {
	mock := &mockGitHubAPI{
		orgID: testOrgID,
		mannequins: []github.Mannequin{
			{ID: "m1", Login: "mona", MappedUser: &github.MannequinUser{ID: "u1", Login: "old_user"}},
		},
		userID: "target-id",
		invitationResult: &github.CreateAttributionInvitationResult{
			Source: &github.Mannequin{ID: "m1", Login: "mona"},
			Target: &github.MannequinUser{ID: "target-id", Login: "target_user"},
		},
	}
	var buf bytes.Buffer
	log := logger.New(false, &buf)
	svc := mannequin.NewReclaimService(mock, log)

	lines := []string{
		mannequin.CSVHeader,
		"mona,m1,target_user",
	}
	// force=true should proceed even though already claimed
	err := svc.ReclaimMannequins(context.Background(), lines, testOrg, true, false)
	require.NoError(t, err)
	require.Len(t, mock.invitations, 1)
}

// ---------- ReclaimMannequin (single mode) tests ----------

func TestReclaimMannequin_UserNotMannequin_ReturnsError(t *testing.T) {
	mock := &mockGitHubAPI{
		orgID:             testOrgID,
		mannequinsByLogin: []github.Mannequin{}, // no mannequins found
	}
	var buf bytes.Buffer
	log := logger.New(false, &buf)
	svc := mannequin.NewReclaimService(mock, log)

	err := svc.ReclaimMannequin(context.Background(), "mona", "", "target", testOrg, false, false)
	require.Error(t, err)
	assert.Contains(t, err.Error(), "is not a mannequin")
}

func TestReclaimMannequin_AlreadyMapped_NotForce_ReturnsError(t *testing.T) {
	mock := &mockGitHubAPI{
		orgID: testOrgID,
		mannequinsByLogin: []github.Mannequin{
			{ID: "m1", Login: "mona", MappedUser: &github.MannequinUser{ID: "u1", Login: "old"}},
		},
	}
	var buf bytes.Buffer
	log := logger.New(false, &buf)
	svc := mannequin.NewReclaimService(mock, log)

	err := svc.ReclaimMannequin(context.Background(), "mona", "", "target", testOrg, false, false)
	require.Error(t, err)
	assert.Contains(t, err.Error(), "already mapped")
}

func TestReclaimMannequin_HappyPath_Invitation(t *testing.T) {
	mock := &mockGitHubAPI{
		orgID: testOrgID,
		mannequinsByLogin: []github.Mannequin{
			{ID: "m1", Login: "mona"},
		},
		userID: "target-id",
		invitationResult: &github.CreateAttributionInvitationResult{
			Source: &github.Mannequin{ID: "m1", Login: "mona"},
			Target: &github.MannequinUser{ID: "target-id", Login: "target_user"},
		},
	}
	var buf bytes.Buffer
	log := logger.New(false, &buf)
	svc := mannequin.NewReclaimService(mock, log)

	err := svc.ReclaimMannequin(context.Background(), "mona", "", "target_user", testOrg, false, false)
	require.NoError(t, err)
	assert.Contains(t, buf.String(), "invitation email successfully sent")
	require.Len(t, mock.invitations, 1)
}

func TestReclaimMannequin_HappyPath_SkipInvitation(t *testing.T) {
	mock := &mockGitHubAPI{
		orgID: testOrgID,
		mannequinsByLogin: []github.Mannequin{
			{ID: "m1", Login: "mona"},
		},
		userID: "target-id",
		reclaimResult: &github.ReattributeMannequinToUserResult{
			Source: &github.Mannequin{ID: "m1", Login: "mona"},
			Target: &github.MannequinUser{ID: "target-id", Login: "target_user"},
		},
	}
	var buf bytes.Buffer
	log := logger.New(false, &buf)
	svc := mannequin.NewReclaimService(mock, log)

	err := svc.ReclaimMannequin(context.Background(), "mona", "", "target_user", testOrg, false, true)
	require.NoError(t, err)
	assert.Contains(t, buf.String(), "Successfully reclaimed")
	require.Len(t, mock.reclaims, 1)
}

func TestReclaimMannequin_WithMannequinID_FiltersCorrectly(t *testing.T) {
	mock := &mockGitHubAPI{
		orgID: testOrgID,
		mannequinsByLogin: []github.Mannequin{
			{ID: "m1", Login: "mona"},
			{ID: "m2", Login: "mona"},
		},
		userID: "target-id",
		invitationResult: &github.CreateAttributionInvitationResult{
			Source: &github.Mannequin{ID: "m2", Login: "mona"},
			Target: &github.MannequinUser{ID: "target-id", Login: "target_user"},
		},
	}
	var buf bytes.Buffer
	log := logger.New(false, &buf)
	svc := mannequin.NewReclaimService(mock, log)

	err := svc.ReclaimMannequin(context.Background(), "mona", "m2", "target_user", testOrg, false, false)
	require.NoError(t, err)
	// Should only have invited one mannequin (m2)
	require.Len(t, mock.invitations, 1)
	assert.Equal(t, "m2", mock.invitations[0].SourceID)
}

func TestReclaimMannequin_InvitationFails_ReturnsError(t *testing.T) {
	mock := &mockGitHubAPI{
		orgID: testOrgID,
		mannequinsByLogin: []github.Mannequin{
			{ID: "m1", Login: "mona"},
		},
		userID: "target-id",
		invitationResult: &github.CreateAttributionInvitationResult{
			Errors: []github.ErrorData{{Message: "some error"}},
		},
	}
	var buf bytes.Buffer
	log := logger.New(false, &buf)
	svc := mannequin.NewReclaimService(mock, log)

	err := svc.ReclaimMannequin(context.Background(), "mona", "", "target_user", testOrg, false, false)
	require.Error(t, err)
	assert.Contains(t, err.Error(), "Failed to send reclaim mannequin invitation(s)")
}

func TestReclaimMannequin_SkipInvitation_EMUError_ReturnsError(t *testing.T) {
	mock := &mockGitHubAPI{
		orgID: testOrgID,
		mannequinsByLogin: []github.Mannequin{
			{ID: "m1", Login: "mona"},
		},
		userID: "target-id",
		reclaimResult: &github.ReattributeMannequinToUserResult{
			Errors: []github.ErrorData{
				{Message: "is not an Enterprise Managed Users (EMU) organization"},
			},
		},
	}
	var buf bytes.Buffer
	log := logger.New(false, &buf)
	svc := mannequin.NewReclaimService(mock, log)

	err := svc.ReclaimMannequin(context.Background(), "mona", "", "target_user", testOrg, false, true)
	require.Error(t, err)
	assert.Contains(t, err.Error(), "Failed to reclaim mannequin")
}

func TestReclaimMannequin_InvitationResultMismatch_ReturnsError(t *testing.T) {
	mock := &mockGitHubAPI{
		orgID: testOrgID,
		mannequinsByLogin: []github.Mannequin{
			{ID: "m1", Login: "mona"},
		},
		userID: "target-id",
		invitationResult: &github.CreateAttributionInvitationResult{
			Source: &github.Mannequin{ID: "wrong-id", Login: "mona"},
			Target: &github.MannequinUser{ID: "target-id", Login: "target_user"},
		},
	}
	var buf bytes.Buffer
	log := logger.New(false, &buf)
	svc := mannequin.NewReclaimService(mock, log)

	err := svc.ReclaimMannequin(context.Background(), "mona", "", "target_user", testOrg, false, false)
	require.Error(t, err)
	assert.Contains(t, err.Error(), "Failed to send reclaim mannequin invitation(s)")
}

func TestReclaimMannequin_Force_AlreadyMapped_Proceeds(t *testing.T) {
	mock := &mockGitHubAPI{
		orgID: testOrgID,
		mannequinsByLogin: []github.Mannequin{
			{ID: "m1", Login: "mona", MappedUser: &github.MannequinUser{ID: "u1", Login: "old"}},
		},
		userID: "target-id",
		invitationResult: &github.CreateAttributionInvitationResult{
			Source: &github.Mannequin{ID: "m1", Login: "mona"},
			Target: &github.MannequinUser{ID: "target-id", Login: "target_user"},
		},
	}
	var buf bytes.Buffer
	log := logger.New(false, &buf)
	svc := mannequin.NewReclaimService(mock, log)

	err := svc.ReclaimMannequin(context.Background(), "mona", "", "target_user", testOrg, true, false)
	require.NoError(t, err)
	require.Len(t, mock.invitations, 1)
}

// ---------- HandleInvitationResult edge cases ----------

func TestReclaimMannequins_Invitation_WithErrors_LogsError(t *testing.T) {
	mock := &mockGitHubAPI{
		orgID: testOrgID,
		mannequins: []github.Mannequin{
			{ID: "m1", Login: "mona"},
		},
		userID: "target-id",
		invitationResult: &github.CreateAttributionInvitationResult{
			Errors: []github.ErrorData{{Message: "invitation error"}},
		},
	}
	var buf bytes.Buffer
	log := logger.New(false, &buf)
	svc := mannequin.NewReclaimService(mock, log)

	lines := []string{
		mannequin.CSVHeader,
		"mona,m1,target_user",
	}
	// In CSV mode, invitation errors are logged but processing continues
	err := svc.ReclaimMannequins(context.Background(), lines, testOrg, false, false)
	require.NoError(t, err)
	assert.Contains(t, buf.String(), "Failed to send reclaim invitation")
}

func TestReclaimMannequins_SkipInvitation_OtherError_ContinuesProcessing(t *testing.T) {
	mock := &mockGitHubAPI{
		orgID: testOrgID,
		mannequins: []github.Mannequin{
			{ID: "m1", Login: "mona"},
			{ID: "m2", Login: "lisa"},
		},
		userID: "target-id",
	}
	mock.reclaimResult = &github.ReattributeMannequinToUserResult{
		Errors: []github.ErrorData{{Message: "some non-EMU error"}},
	}

	var buf bytes.Buffer
	log := logger.New(false, &buf)
	svc := mannequin.NewReclaimService(mock, log)

	lines := []string{
		mannequin.CSVHeader,
		"mona,m1,target_user",
		"lisa,m2,target_user",
	}
	err := svc.ReclaimMannequins(context.Background(), lines, testOrg, false, true)
	require.NoError(t, err)
	// Should have tried both reclaims (non-EMU errors continue)
	assert.Len(t, mock.reclaims, 2)
}
