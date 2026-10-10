package logger_test

import (
	"bytes"
	"errors"
	"strings"
	"testing"

	"github.com/github/gh-gei/pkg/logger"
)

func TestLogger_Info(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	log.Info("test message")

	output := buf.String()
	if !strings.Contains(output, "[INFO]") {
		t.Errorf("expected [INFO] in output, got: %s", output)
	}
	if !strings.Contains(output, "test message") {
		t.Errorf("expected 'test message' in output, got: %s", output)
	}
}

func TestLogger_Warning(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	log.Warning("warning 1")
	log.Warning("warning 2")

	if log.GetWarningCount() != 2 {
		t.Errorf("expected 2 warnings, got: %d", log.GetWarningCount())
	}

	output := buf.String()
	if !strings.Contains(output, "[WARNING]") {
		t.Errorf("expected [WARNING] in output, got: %s", output)
	}
}

func TestLogger_Error(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	testErr := errors.New("test error")
	log.Error(testErr)

	output := buf.String()
	if !strings.Contains(output, "[ERROR]") {
		t.Errorf("expected [ERROR] in output, got: %s", output)
	}
	if !strings.Contains(output, "test error") {
		t.Errorf("expected 'test error' in output, got: %s", output)
	}
}

func TestLogger_Verbose(t *testing.T) {
	tests := []struct {
		name           string
		verbose        bool
		message        string
		expectInOutput bool
	}{
		{
			name:           "verbose enabled",
			verbose:        true,
			message:        "verbose message",
			expectInOutput: true,
		},
		{
			name:           "verbose disabled",
			verbose:        false,
			message:        "verbose message",
			expectInOutput: false,
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			var buf bytes.Buffer
			log := logger.New(tt.verbose, &buf, &buf)

			log.Verbose("%s", tt.message)

			output := buf.String()
			contains := strings.Contains(output, tt.message)

			if tt.expectInOutput && !contains {
				t.Errorf("expected message in output when verbose=%v, got: %s", tt.verbose, output)
			}
			if !tt.expectInOutput && contains {
				t.Errorf("expected no message in output when verbose=%v, got: %s", tt.verbose, output)
			}
		})
	}
}

func TestLogger_ErrorWithNil(t *testing.T) {
	var buf bytes.Buffer
	log := logger.New(false, &buf)

	log.Error(nil)

	output := buf.String()
	if output != "" {
		t.Errorf("expected no output for nil error, got: %s", output)
	}
}
