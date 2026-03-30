package main

import (
	"bytes"
	"context"
	"fmt"
	"testing"

	"github.com/github/gh-gei/pkg/logger"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

// mockMannequinReclaimer implements mannequinReclaimer for testing.
type mockMannequinReclaimer struct {
	reclaimErr  error
	reclaimsErr error

	gotReclaimArgs  *reclaimSingleArgs
	gotReclaimsArgs *reclaimBulkArgs
}

type reclaimSingleArgs struct {
	MannequinUser  string
	MannequinID    string
	TargetUser     string
	Org            string
	Force          bool
	SkipInvitation bool
}

type reclaimBulkArgs struct {
	Lines          []string
	Org            string
	Force          bool
	SkipInvitation bool
}

func (m *mockMannequinReclaimer) ReclaimMannequin(_ context.Context, mannequinUser, mannequinID, targetUser, org string, force, skipInvitation bool) error {
	m.gotReclaimArgs = &reclaimSingleArgs{
		MannequinUser:  mannequinUser,
		MannequinID:    mannequinID,
		TargetUser:     targetUser,
		Org:            org,
		Force:          force,
		SkipInvitation: skipInvitation,
	}
	return m.reclaimErr
}

func (m *mockMannequinReclaimer) ReclaimMannequins(_ context.Context, lines []string, org string, force, skipInvitation bool) error {
	m.gotReclaimsArgs = &reclaimBulkArgs{
		Lines:          lines,
		Org:            org,
		Force:          force,
		SkipInvitation: skipInvitation,
	}
	return m.reclaimsErr
}

// mockMannequinReclaimAPI implements mannequinReclaimAPI for testing.
type mockMannequinReclaimAPI struct {
	loginName     string
	loginNameErr  error
	membership    string
	membershipErr error
}

func (m *mockMannequinReclaimAPI) GetLoginName(_ context.Context) (string, error) {
	return m.loginName, m.loginNameErr
}

func (m *mockMannequinReclaimAPI) GetOrgMembershipForUser(_ context.Context, org, member string) (string, error) {
	return m.membership, m.membershipErr
}

func TestReclaimMannequin(t *testing.T) {
	tests := []struct {
		name        string
		args        []string
		reclaimer   *mockMannequinReclaimer
		api         *mockMannequinReclaimAPI
		fileExists  func(string) bool
		readFile    func(string) ([]string, error)
		wantErr     string
		wantOutput  []string
		assertCalls func(t *testing.T, r *mockMannequinReclaimer)
	}{
		{
			name:       "single reclaim happy path",
			args:       []string{"--github-target-org", "FooOrg", "--mannequin-user", "mona", "--target-user", "mona_emu"},
			reclaimer:  &mockMannequinReclaimer{},
			api:        &mockMannequinReclaimAPI{},
			wantOutput: []string{"Reclaiming Mannequin..."},
			assertCalls: func(t *testing.T, r *mockMannequinReclaimer) {
				require.NotNil(t, r.gotReclaimArgs)
				assert.Equal(t, "mona", r.gotReclaimArgs.MannequinUser)
				assert.Equal(t, "", r.gotReclaimArgs.MannequinID)
				assert.Equal(t, "mona_emu", r.gotReclaimArgs.TargetUser)
				assert.Equal(t, "FooOrg", r.gotReclaimArgs.Org)
				assert.False(t, r.gotReclaimArgs.Force)
				assert.False(t, r.gotReclaimArgs.SkipInvitation)
			},
		},
		{
			name:      "single reclaim with mannequin ID",
			args:      []string{"--github-target-org", "FooOrg", "--mannequin-user", "mona", "--mannequin-id", "m1", "--target-user", "mona_emu"},
			reclaimer: &mockMannequinReclaimer{},
			api:       &mockMannequinReclaimAPI{},
			assertCalls: func(t *testing.T, r *mockMannequinReclaimer) {
				require.NotNil(t, r.gotReclaimArgs)
				assert.Equal(t, "m1", r.gotReclaimArgs.MannequinID)
			},
		},
		{
			name:       "CSV reclaim happy path",
			args:       []string{"--github-target-org", "FooOrg", "--csv", "file.csv"},
			reclaimer:  &mockMannequinReclaimer{},
			api:        &mockMannequinReclaimAPI{},
			fileExists: func(string) bool { return true },
			readFile: func(string) ([]string, error) {
				return []string{"header", "line1"}, nil
			},
			wantOutput: []string{"Reclaiming Mannequins with CSV"},
			assertCalls: func(t *testing.T, r *mockMannequinReclaimer) {
				require.NotNil(t, r.gotReclaimsArgs)
				assert.Equal(t, []string{"header", "line1"}, r.gotReclaimsArgs.Lines)
				assert.Equal(t, "FooOrg", r.gotReclaimsArgs.Org)
			},
		},
		{
			name:       "CSV takes precedence over single",
			args:       []string{"--github-target-org", "FooOrg", "--csv", "file.csv", "--mannequin-user", "mona", "--target-user", "target"},
			reclaimer:  &mockMannequinReclaimer{},
			api:        &mockMannequinReclaimAPI{},
			fileExists: func(string) bool { return true },
			readFile:   func(string) ([]string, error) { return []string{}, nil },
			assertCalls: func(t *testing.T, r *mockMannequinReclaimer) {
				require.NotNil(t, r.gotReclaimsArgs, "should use CSV mode")
				assert.Nil(t, r.gotReclaimArgs, "should not use single mode")
			},
		},
		{
			name:       "CSV file does not exist",
			args:       []string{"--github-target-org", "FooOrg", "--csv", "nonexistent.csv"},
			reclaimer:  &mockMannequinReclaimer{},
			api:        &mockMannequinReclaimAPI{},
			fileExists: func(string) bool { return false },
			wantErr:    "does not exist",
		},
		{
			name:      "skip-invitation with admin user and no-prompt",
			args:      []string{"--github-target-org", "FooOrg", "--csv", "file.csv", "--skip-invitation", "--no-prompt"},
			reclaimer: &mockMannequinReclaimer{},
			api: &mockMannequinReclaimAPI{
				loginName:  "admin_user",
				membership: "admin",
			},
			fileExists: func(string) bool { return true },
			readFile:   func(string) ([]string, error) { return []string{}, nil },
			assertCalls: func(t *testing.T, r *mockMannequinReclaimer) {
				require.NotNil(t, r.gotReclaimsArgs)
				assert.True(t, r.gotReclaimsArgs.SkipInvitation)
			},
		},
		{
			name:      "skip-invitation without no-prompt returns error",
			args:      []string{"--github-target-org", "FooOrg", "--csv", "file.csv", "--skip-invitation"},
			reclaimer: &mockMannequinReclaimer{},
			api: &mockMannequinReclaimAPI{
				loginName:  "admin_user",
				membership: "admin",
			},
			wantErr: "--no-prompt",
		},
		{
			name:      "skip-invitation non-admin returns error",
			args:      []string{"--github-target-org", "FooOrg", "--csv", "file.csv", "--skip-invitation", "--no-prompt"},
			reclaimer: &mockMannequinReclaimer{},
			api: &mockMannequinReclaimAPI{
				loginName:  "regular_user",
				membership: "member",
			},
			wantErr: "not an org admin",
		},
		{
			name:      "missing org flag",
			args:      []string{},
			reclaimer: &mockMannequinReclaimer{},
			api:       &mockMannequinReclaimAPI{},
			wantErr:   "--github-target-org must be provided",
		},
		{
			name:      "org is URL",
			args:      []string{"--github-target-org", "https://github.com/my-org", "--mannequin-user", "m", "--target-user", "t"},
			reclaimer: &mockMannequinReclaimer{},
			api:       &mockMannequinReclaimAPI{},
			wantErr:   "expects an organization name, not a URL",
		},
		{
			name:      "neither csv nor mannequin-user/target-user",
			args:      []string{"--github-target-org", "FooOrg"},
			reclaimer: &mockMannequinReclaimer{},
			api:       &mockMannequinReclaimAPI{},
			wantErr:   "Either --csv or --mannequin-user and --target-user must be specified",
		},
		{
			name:      "mannequin-user without target-user",
			args:      []string{"--github-target-org", "FooOrg", "--mannequin-user", "mona"},
			reclaimer: &mockMannequinReclaimer{},
			api:       &mockMannequinReclaimAPI{},
			wantErr:   "Either --csv or --mannequin-user and --target-user must be specified",
		},
		{
			name:      "force flag passed through",
			args:      []string{"--github-target-org", "FooOrg", "--mannequin-user", "mona", "--target-user", "target", "--force"},
			reclaimer: &mockMannequinReclaimer{},
			api:       &mockMannequinReclaimAPI{},
			assertCalls: func(t *testing.T, r *mockMannequinReclaimer) {
				require.NotNil(t, r.gotReclaimArgs)
				assert.True(t, r.gotReclaimArgs.Force)
			},
		},
		{
			name:      "reclaim service error propagates",
			args:      []string{"--github-target-org", "FooOrg", "--mannequin-user", "mona", "--target-user", "target"},
			reclaimer: &mockMannequinReclaimer{reclaimErr: fmt.Errorf("reclaim failed")},
			api:       &mockMannequinReclaimAPI{},
			wantErr:   "reclaim failed",
		},
	}

	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			var buf bytes.Buffer
			log := logger.New(false, &buf)

			fileExists := tc.fileExists
			if fileExists == nil {
				fileExists = func(string) bool { return true }
			}
			readFile := tc.readFile
			if readFile == nil {
				readFile = func(string) ([]string, error) { return nil, nil }
			}

			cmd := newReclaimMannequinCmd(tc.reclaimer, tc.api, log, fileExists, readFile)
			cmd.SetOut(&buf)
			cmd.SetErr(&buf)
			cmd.SetArgs(tc.args)

			err := cmd.Execute()

			if tc.wantErr != "" {
				require.Error(t, err)
				assert.Contains(t, err.Error(), tc.wantErr)
			} else {
				require.NoError(t, err)
			}

			output := buf.String()
			for _, want := range tc.wantOutput {
				assert.Contains(t, output, want, "expected output to contain %q", want)
			}

			if tc.assertCalls != nil {
				tc.assertCalls(t, tc.reclaimer)
			}
		})
	}
}
