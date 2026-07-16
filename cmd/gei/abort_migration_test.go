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

// mockMigrationAborter implements the migrationAborter interface for testing.
type mockMigrationAborter struct {
	result bool
	err    error
	called bool
	gotID  string
}

func (m *mockMigrationAborter) AbortMigration(_ context.Context, id string) (bool, error) {
	m.called = true
	m.gotID = id
	return m.result, m.err
}

func TestAbortMigration(t *testing.T) {
	tests := []struct {
		name        string
		migrationID string
		mock        *mockMigrationAborter
		wantErr     string
		wantOutput  []string // substrings that must appear in output
		wantCalled  bool
		wantID      string // expected migration ID passed to mock
	}{
		{
			name:        "abort succeeds",
			migrationID: "RM_123",
			mock:        &mockMigrationAborter{result: true},
			wantOutput:  []string{"Migration RM_123 was canceled"},
			wantCalled:  true,
			wantID:      "RM_123",
		},
		{
			name:        "abort fails returns false",
			migrationID: "RM_456",
			mock:        &mockMigrationAborter{result: false},
			wantOutput:  []string{"Failed to abort migration RM_456"},
			wantCalled:  true,
		},
		{
			name:        "abort returns error",
			migrationID: "RM_789",
			mock:        &mockMigrationAborter{err: fmt.Errorf("network failure")},
			wantErr:     "network failure",
			wantCalled:  true,
		},
		{
			name:        "missing migration ID",
			migrationID: "",
			mock:        &mockMigrationAborter{},
			wantErr:     "--migration-id must be provided",
			wantCalled:  false,
		},
		{
			name:        "invalid migration ID no RM_ prefix",
			migrationID: "XX_invalid",
			mock:        &mockMigrationAborter{},
			wantErr:     "Invalid migration ID: XX_invalid. Only repository migration IDs starting with RM_ are supported.",
			wantCalled:  false,
		},
		{
			name:        "OM_ prefix rejected for abort",
			migrationID: "OM_100",
			mock:        &mockMigrationAborter{},
			wantErr:     "Invalid migration ID: OM_100. Only repository migration IDs starting with RM_ are supported.",
			wantCalled:  false,
		},
	}

	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			var buf bytes.Buffer
			log := logger.New(false, &buf)

			cmd := newAbortMigrationCmd(tc.mock, log)
			cmd.SetOut(&buf)
			cmd.SetErr(&buf)

			args := []string{}
			if tc.migrationID != "" {
				args = append(args, "--migration-id", tc.migrationID)
			}
			cmd.SetArgs(args)

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

			assert.Equal(t, tc.wantCalled, tc.mock.called, "expected AbortMigration called=%v", tc.wantCalled)
			if tc.wantID != "" {
				assert.Equal(t, tc.wantID, tc.mock.gotID)
			}
		})
	}
}
