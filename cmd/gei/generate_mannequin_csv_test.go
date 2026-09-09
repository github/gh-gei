package main

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

type mockMannequinCSVGenerator struct {
	orgID    string
	orgIDErr error

	mannequins    []github.Mannequin
	mannequinsErr error
}

func (m *mockMannequinCSVGenerator) GetOrganizationId(_ context.Context, org string) (string, error) {
	return m.orgID, m.orgIDErr
}

func (m *mockMannequinCSVGenerator) GetMannequins(_ context.Context, orgID string) ([]github.Mannequin, error) {
	return m.mannequins, m.mannequinsErr
}

func TestGenerateMannequinCSV(t *testing.T) {
	tests := []struct {
		name           string
		args           []string
		mock           *mockMannequinCSVGenerator
		wantErr        string
		wantOutput     []string
		wantCSVContent string
	}{
		{
			name: "no mannequins generates CSV with header only",
			args: []string{"--github-target-org", "FooOrg", "--output", "test.csv"},
			mock: &mockMannequinCSVGenerator{
				orgID:      "org-id",
				mannequins: []github.Mannequin{},
			},
			wantOutput: []string{
				"Generating CSV",
				"# Mannequins Found: 0",
				"# Mannequins Previously Reclaimed: 0",
			},
			wantCSVContent: mannequin.CSVHeader + "\n",
		},
		{
			name: "mannequins without reclaimed, exclude reclaimed",
			args: []string{"--github-target-org", "FooOrg"},
			mock: &mockMannequinCSVGenerator{
				orgID: "org-id",
				mannequins: []github.Mannequin{
					{ID: "monaid", Login: "mona"},
					{ID: "monalisaid", Login: "monalisa", MappedUser: &github.MannequinUser{ID: "mapped-id", Login: "monalisa_gh"}},
				},
			},
			wantOutput: []string{
				"# Mannequins Found: 2",
				"# Mannequins Previously Reclaimed: 1",
			},
			wantCSVContent: mannequin.CSVHeader + "\n" +
				"mona,monaid,\n",
		},
		{
			name: "include reclaimed mannequins",
			args: []string{"--github-target-org", "FooOrg", "--include-reclaimed"},
			mock: &mockMannequinCSVGenerator{
				orgID: "org-id",
				mannequins: []github.Mannequin{
					{ID: "monaid", Login: "mona"},
					{ID: "monalisaid", Login: "monalisa", MappedUser: &github.MannequinUser{ID: "mapped-id", Login: "monalisa_gh"}},
				},
			},
			wantCSVContent: mannequin.CSVHeader + "\n" +
				"mona,monaid,\n" +
				"monalisa,monalisaid,monalisa_gh\n",
		},
		{
			name:    "missing github-target-org flag",
			args:    []string{},
			mock:    &mockMannequinCSVGenerator{},
			wantErr: "--github-target-org must be provided",
		},
		{
			name:    "github-target-org is URL",
			args:    []string{"--github-target-org", "https://github.com/my-org"},
			mock:    &mockMannequinCSVGenerator{},
			wantErr: "expects an organization name, not a URL",
		},
		{
			name: "GetOrganizationId error propagates",
			args: []string{"--github-target-org", "FooOrg"},
			mock: &mockMannequinCSVGenerator{
				orgIDErr: fmt.Errorf("org not found"),
			},
			wantErr: "org not found",
		},
		{
			name: "GetMannequins error propagates",
			args: []string{"--github-target-org", "FooOrg"},
			mock: &mockMannequinCSVGenerator{
				orgID:         "org-id",
				mannequinsErr: fmt.Errorf("api error"),
			},
			wantErr: "api error",
		},
	}

	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			var buf bytes.Buffer
			log := logger.New(false, &buf)

			var writtenContent string
			writeFile := func(path, content string) error {
				writtenContent = content
				return nil
			}

			cmd := newGenerateMannequinCSVCmd(tc.mock, log, writeFile)
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

			if tc.wantCSVContent != "" {
				assert.Equal(t, tc.wantCSVContent, writtenContent)
			}
		})
	}
}
