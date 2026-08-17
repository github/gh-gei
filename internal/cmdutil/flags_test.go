package cmdutil_test

import (
	"errors"
	"testing"

	"github.com/github/gh-gei/internal/cmdutil"
)

func TestIsURL(t *testing.T) {
	tests := []struct {
		name  string
		input string
		want  bool
	}{
		{"http url", "http://example.com", true},
		{"https url", "https://example.com", true},
		{"https with path", "https://github.com/org", true},
		{"not a url", "my-org", false},
		{"empty string", "", false},
		{"ftp scheme", "ftp://example.com", false},
		{"just a word", "github", false},
		{"url-like but no scheme", "example.com/path", false},
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			if got := cmdutil.IsURL(tt.input); got != tt.want {
				t.Errorf("IsURL(%q) = %v, want %v", tt.input, got, tt.want)
			}
		})
	}
}

func TestValidateNoURL(t *testing.T) {
	t.Run("returns nil for non-URL value", func(t *testing.T) {
		if err := cmdutil.ValidateNoURL("my-org", "--github-org"); err != nil {
			t.Errorf("expected nil, got %v", err)
		}
	})
	t.Run("returns nil for empty value", func(t *testing.T) {
		if err := cmdutil.ValidateNoURL("", "--github-org"); err != nil {
			t.Errorf("expected nil, got %v", err)
		}
	})
	t.Run("returns UserError for URL value", func(t *testing.T) {
		err := cmdutil.ValidateNoURL("https://github.com/org", "--github-org")
		if err == nil {
			t.Fatal("expected error, got nil")
		}
		var ue *cmdutil.UserError
		if !errors.As(err, &ue) {
			t.Fatalf("expected UserError, got %T", err)
		}
	})
}

func TestValidateRequired(t *testing.T) {
	t.Run("returns nil for non-empty value", func(t *testing.T) {
		if err := cmdutil.ValidateRequired("val", "--flag"); err != nil {
			t.Errorf("expected nil, got %v", err)
		}
	})
	t.Run("returns UserError for empty value", func(t *testing.T) {
		err := cmdutil.ValidateRequired("", "--flag")
		if err == nil {
			t.Fatal("expected error, got nil")
		}
		var ue *cmdutil.UserError
		if !errors.As(err, &ue) {
			t.Fatalf("expected UserError, got %T", err)
		}
	})
	t.Run("returns UserError for whitespace-only value", func(t *testing.T) {
		err := cmdutil.ValidateRequired("   ", "--flag")
		if err == nil {
			t.Fatal("expected error, got nil")
		}
	})
}

func TestValidateMutuallyExclusive(t *testing.T) {
	t.Run("returns nil when neither set", func(t *testing.T) {
		if err := cmdutil.ValidateMutuallyExclusive("", "--a", "", "--b"); err != nil {
			t.Errorf("expected nil, got %v", err)
		}
	})
	t.Run("returns nil when only first set", func(t *testing.T) {
		if err := cmdutil.ValidateMutuallyExclusive("val", "--a", "", "--b"); err != nil {
			t.Errorf("expected nil, got %v", err)
		}
	})
	t.Run("returns nil when only second set", func(t *testing.T) {
		if err := cmdutil.ValidateMutuallyExclusive("", "--a", "val", "--b"); err != nil {
			t.Errorf("expected nil, got %v", err)
		}
	})
	t.Run("returns UserError when both set", func(t *testing.T) {
		err := cmdutil.ValidateMutuallyExclusive("v1", "--a", "v2", "--b")
		if err == nil {
			t.Fatal("expected error, got nil")
		}
		var ue *cmdutil.UserError
		if !errors.As(err, &ue) {
			t.Fatalf("expected UserError, got %T", err)
		}
	})
}

func TestValidatePaired(t *testing.T) {
	t.Run("returns nil when both set", func(t *testing.T) {
		if err := cmdutil.ValidatePaired("a", "--git-archive-url", "b", "--metadata-archive-url"); err != nil {
			t.Errorf("expected nil, got %v", err)
		}
	})
	t.Run("returns nil when neither set", func(t *testing.T) {
		if err := cmdutil.ValidatePaired("", "--git-archive-url", "", "--metadata-archive-url"); err != nil {
			t.Errorf("expected nil, got %v", err)
		}
	})
	t.Run("returns UserError when only first set", func(t *testing.T) {
		err := cmdutil.ValidatePaired("a", "--git-archive-url", "", "--metadata-archive-url")
		if err == nil {
			t.Fatal("expected error, got nil")
		}
		var ue *cmdutil.UserError
		if !errors.As(err, &ue) {
			t.Fatalf("expected UserError, got %T", err)
		}
	})
	t.Run("returns UserError when only second set", func(t *testing.T) {
		err := cmdutil.ValidatePaired("", "--git-archive-url", "b", "--metadata-archive-url")
		if err == nil {
			t.Fatal("expected error, got nil")
		}
		var ue *cmdutil.UserError
		if !errors.As(err, &ue) {
			t.Fatalf("expected UserError, got %T", err)
		}
	})
}

func TestValidateRequiredWhen(t *testing.T) {
	t.Run("returns nil when condition is false", func(t *testing.T) {
		if err := cmdutil.ValidateRequiredWhen("", "--flag", false, "--other is set"); err != nil {
			t.Errorf("expected nil, got %v", err)
		}
	})
	t.Run("returns nil when condition is true and value is set", func(t *testing.T) {
		if err := cmdutil.ValidateRequiredWhen("val", "--flag", true, "--other is set"); err != nil {
			t.Errorf("expected nil, got %v", err)
		}
	})
	t.Run("returns UserError when condition is true and value is empty", func(t *testing.T) {
		err := cmdutil.ValidateRequiredWhen("", "--ghes-api-url", true, "--no-ssl-verify is specified")
		if err == nil {
			t.Fatal("expected error, got nil")
		}
		var ue *cmdutil.UserError
		if !errors.As(err, &ue) {
			t.Fatalf("expected UserError, got %T", err)
		}
	})
}

func TestValidateOneOf(t *testing.T) {
	t.Run("returns nil for valid value", func(t *testing.T) {
		if err := cmdutil.ValidateOneOf("TEAM", "--actor-type", "TEAM", "USER"); err != nil {
			t.Errorf("expected nil, got %v", err)
		}
	})
	t.Run("returns nil for valid value case-insensitive", func(t *testing.T) {
		if err := cmdutil.ValidateOneOf("team", "--actor-type", "TEAM", "USER"); err != nil {
			t.Errorf("expected nil, got %v", err)
		}
	})
	t.Run("returns nil for empty value", func(t *testing.T) {
		// empty means the flag was not set, so no validation needed
		if err := cmdutil.ValidateOneOf("", "--actor-type", "TEAM", "USER"); err != nil {
			t.Errorf("expected nil, got %v", err)
		}
	})
	t.Run("returns UserError for invalid value", func(t *testing.T) {
		err := cmdutil.ValidateOneOf("INVALID", "--actor-type", "TEAM", "USER")
		if err == nil {
			t.Fatal("expected error, got nil")
		}
		var ue *cmdutil.UserError
		if !errors.As(err, &ue) {
			t.Fatalf("expected UserError, got %T", err)
		}
	})
}
