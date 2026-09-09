package main

import (
	"bytes"
	"context"
	"fmt"
	"testing"

	"github.com/github/gh-gei/pkg/github"
	"github.com/github/gh-gei/pkg/logger"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

// mockTeamCreator implements teamCreator for testing.
type mockTeamCreator struct {
	teams           []github.Team
	getTeamsErr     error
	createdTeam     *github.Team
	createTeamErr   error
	teamMembers     []string
	getMembersErr   error
	removeMemberErr error
	idpGroupID      int
	getIdpErr       error
	addEmuErr       error

	// capture calls
	gotCreateOrg   string
	gotCreateName  string
	gotMembersOrg  string
	gotMembersSlug string
	removedMembers []string
	gotIdpOrg      string
	gotIdpGroup    string
	gotEmuOrg      string
	gotEmuSlug     string
	gotEmuGroupID  int
}

func (m *mockTeamCreator) GetTeams(_ context.Context, org string) ([]github.Team, error) {
	return m.teams, m.getTeamsErr
}

func (m *mockTeamCreator) CreateTeam(_ context.Context, org, name string) (*github.Team, error) {
	m.gotCreateOrg = org
	m.gotCreateName = name
	return m.createdTeam, m.createTeamErr
}

func (m *mockTeamCreator) GetTeamMembers(_ context.Context, org, teamSlug string) ([]string, error) {
	m.gotMembersOrg = org
	m.gotMembersSlug = teamSlug
	return m.teamMembers, m.getMembersErr
}

func (m *mockTeamCreator) RemoveTeamMember(_ context.Context, org, teamSlug, member string) error {
	m.removedMembers = append(m.removedMembers, member)
	return m.removeMemberErr
}

func (m *mockTeamCreator) GetIdpGroupId(_ context.Context, org, groupName string) (int, error) {
	m.gotIdpOrg = org
	m.gotIdpGroup = groupName
	return m.idpGroupID, m.getIdpErr
}

func (m *mockTeamCreator) AddEmuGroupToTeam(_ context.Context, org, teamSlug string, groupID int) error {
	m.gotEmuOrg = org
	m.gotEmuSlug = teamSlug
	m.gotEmuGroupID = groupID
	return m.addEmuErr
}

func TestCreateTeam(t *testing.T) {
	tests := []struct {
		name       string
		args       []string
		mock       *mockTeamCreator
		wantErr    string
		wantOutput []string
		assertArgs func(t *testing.T, m *mockTeamCreator)
	}{
		{
			name: "team does not exist, creates team, no IdP group",
			args: []string{"--github-org", "my-org", "--team-name", "my-team"},
			mock: &mockTeamCreator{
				teams:       []github.Team{},
				createdTeam: &github.Team{ID: "1", Name: "my-team", Slug: "my-team"},
			},
			wantOutput: []string{
				"Creating GitHub team...",
				"Successfully created team",
				"No IdP Group provided, skipping the IdP linking step",
			},
			assertArgs: func(t *testing.T, m *mockTeamCreator) {
				assert.Equal(t, "my-org", m.gotCreateOrg)
				assert.Equal(t, "my-team", m.gotCreateName)
			},
		},
		{
			name: "team already exists, logs and skips creation",
			args: []string{"--github-org", "my-org", "--team-name", "existing-team"},
			mock: &mockTeamCreator{
				teams: []github.Team{
					{ID: "10", Name: "existing-team", Slug: "existing-team-slug"},
					{ID: "20", Name: "other-team", Slug: "other-slug"},
				},
			},
			wantOutput: []string{
				"Creating GitHub team...",
				"Team 'existing-team' already exists. New team will not be created",
				"No IdP Group provided, skipping the IdP linking step",
			},
			assertArgs: func(t *testing.T, m *mockTeamCreator) {
				// CreateTeam should NOT be called
				assert.Empty(t, m.gotCreateOrg)
				assert.Empty(t, m.gotCreateName)
			},
		},
		{
			name: "team does not exist, creates team, links IdP group with members removed",
			args: []string{"--github-org", "my-org", "--team-name", "my-team", "--idp-group", "my-idp-group"},
			mock: &mockTeamCreator{
				teams:       []github.Team{},
				createdTeam: &github.Team{ID: "1", Name: "my-team", Slug: "my-team-slug"},
				teamMembers: []string{"user1", "user2"},
				idpGroupID:  42,
			},
			wantOutput: []string{
				"Creating GitHub team...",
				"Successfully created team",
				"Successfully linked team to Idp group",
			},
			assertArgs: func(t *testing.T, m *mockTeamCreator) {
				assert.Equal(t, "my-org", m.gotCreateOrg)
				assert.Equal(t, "my-team", m.gotCreateName)
				// Members should be removed
				assert.Equal(t, []string{"user1", "user2"}, m.removedMembers)
				// IdP group looked up and linked
				assert.Equal(t, "my-org", m.gotIdpOrg)
				assert.Equal(t, "my-idp-group", m.gotIdpGroup)
				assert.Equal(t, "my-org", m.gotEmuOrg)
				assert.Equal(t, "my-team-slug", m.gotEmuSlug)
				assert.Equal(t, 42, m.gotEmuGroupID)
			},
		},
		{
			name: "team already exists, links IdP group",
			args: []string{"--github-org", "my-org", "--team-name", "existing-team", "--idp-group", "idp-group"},
			mock: &mockTeamCreator{
				teams: []github.Team{
					{ID: "10", Name: "existing-team", Slug: "existing-slug"},
				},
				teamMembers: []string{"member1"},
				idpGroupID:  99,
			},
			wantOutput: []string{
				"Team 'existing-team' already exists. New team will not be created",
				"Successfully linked team to Idp group",
			},
			assertArgs: func(t *testing.T, m *mockTeamCreator) {
				// Should use slug from existing team
				assert.Equal(t, "existing-slug", m.gotMembersSlug)
				assert.Equal(t, "existing-slug", m.gotEmuSlug)
				assert.Equal(t, []string{"member1"}, m.removedMembers)
			},
		},
		{
			name:    "GetTeams error propagates",
			args:    []string{"--github-org", "my-org", "--team-name", "my-team"},
			mock:    &mockTeamCreator{getTeamsErr: fmt.Errorf("api error")},
			wantErr: "api error",
		},
		{
			name: "CreateTeam error propagates",
			args: []string{"--github-org", "my-org", "--team-name", "my-team"},
			mock: &mockTeamCreator{
				teams:         []github.Team{},
				createTeamErr: fmt.Errorf("create failed"),
			},
			wantErr: "create failed",
		},
		{
			name: "GetTeamMembers error propagates",
			args: []string{"--github-org", "my-org", "--team-name", "my-team", "--idp-group", "grp"},
			mock: &mockTeamCreator{
				teams:         []github.Team{},
				createdTeam:   &github.Team{ID: "1", Name: "my-team", Slug: "my-team"},
				getMembersErr: fmt.Errorf("members error"),
			},
			wantErr: "members error",
		},
		{
			name: "RemoveTeamMember error propagates",
			args: []string{"--github-org", "my-org", "--team-name", "my-team", "--idp-group", "grp"},
			mock: &mockTeamCreator{
				teams:           []github.Team{},
				createdTeam:     &github.Team{ID: "1", Name: "my-team", Slug: "my-team"},
				teamMembers:     []string{"user1"},
				removeMemberErr: fmt.Errorf("remove error"),
			},
			wantErr: "remove error",
		},
		{
			name: "GetIdpGroupId error propagates",
			args: []string{"--github-org", "my-org", "--team-name", "my-team", "--idp-group", "grp"},
			mock: &mockTeamCreator{
				teams:       []github.Team{},
				createdTeam: &github.Team{ID: "1", Name: "my-team", Slug: "my-team"},
				teamMembers: []string{},
				getIdpErr:   fmt.Errorf("idp error"),
			},
			wantErr: "idp error",
		},
		{
			name: "AddEmuGroupToTeam error propagates",
			args: []string{"--github-org", "my-org", "--team-name", "my-team", "--idp-group", "grp"},
			mock: &mockTeamCreator{
				teams:       []github.Team{},
				createdTeam: &github.Team{ID: "1", Name: "my-team", Slug: "my-team"},
				teamMembers: []string{},
				idpGroupID:  10,
				addEmuErr:   fmt.Errorf("emu error"),
			},
			wantErr: "emu error",
		},
		{
			name:    "github-org is URL",
			args:    []string{"--github-org", "https://github.com/my-org", "--team-name", "my-team"},
			mock:    &mockTeamCreator{},
			wantErr: "The --github-org option expects an organization name, not a URL",
		},
		{
			name:    "empty github-org",
			args:    []string{"--github-org", "", "--team-name", "my-team"},
			mock:    &mockTeamCreator{},
			wantErr: "--github-org must be provided",
		},
		{
			name:    "empty team-name",
			args:    []string{"--github-org", "my-org", "--team-name", ""},
			mock:    &mockTeamCreator{},
			wantErr: "--team-name must be provided",
		},
	}

	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			var buf bytes.Buffer
			log := logger.New(false, &buf)

			cmd := newCreateTeamCmd(tc.mock, log)
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

			if tc.assertArgs != nil {
				tc.assertArgs(t, tc.mock)
			}
		})
	}
}
