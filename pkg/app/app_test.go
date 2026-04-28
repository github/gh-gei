package app_test

import (
	"testing"

	"github.com/github/gh-gei/pkg/app"
)

func TestNew(t *testing.T) {
	cfg := &app.Config{
		Verbose:       true,
		RetryAttempts: 5,
	}

	a := app.New(cfg)

	if a == nil {
		t.Fatal("expected app to be created, got nil")
	}

	if a.Logger == nil {
		t.Error("expected logger to be initialized")
	}

	if a.Env == nil {
		t.Error("expected env provider to be initialized")
	}

	if a.FileSystem == nil {
		t.Error("expected filesystem provider to be initialized")
	}

	if a.Retry == nil {
		t.Error("expected retry policy to be initialized")
	}
}

func TestNew_WithDefaults(t *testing.T) {
	cfg := &app.Config{}
	a := app.New(cfg)

	if a == nil {
		t.Fatal("expected app to be created with defaults, got nil")
	}

	// All dependencies should still be initialized even with empty config
	if a.Logger == nil || a.Env == nil || a.FileSystem == nil || a.Retry == nil {
		t.Error("expected all dependencies to be initialized with defaults")
	}
}
