package cmdutil_test

import (
	"errors"
	"fmt"
	"testing"

	"github.com/github/gh-gei/internal/cmdutil"
)

func TestUserError_Error_ReturnsMessage(t *testing.T) {
	ue := &cmdutil.UserError{Message: "something went wrong"}
	if got := ue.Error(); got != "something went wrong" {
		t.Errorf("Error() = %q, want %q", got, "something went wrong")
	}
}

func TestUserError_Unwrap_ReturnsInnerError(t *testing.T) {
	inner := errors.New("inner cause")
	ue := &cmdutil.UserError{Message: "outer", Err: inner}
	got := ue.Unwrap()
	if !errors.Is(got, inner) {
		t.Errorf("Unwrap() = %v, want %v", got, inner)
	}
}

func TestUserError_Unwrap_ReturnsNilWhenNoInnerError(t *testing.T) {
	ue := &cmdutil.UserError{Message: "no inner"}
	if got := ue.Unwrap(); got != nil {
		t.Errorf("Unwrap() = %v, want nil", got)
	}
}

func TestUserError_ErrorsIs_MatchesInnerError(t *testing.T) {
	inner := errors.New("root cause")
	ue := &cmdutil.UserError{Message: "wrapper", Err: inner}
	if !errors.Is(ue, inner) {
		t.Error("errors.Is(userErr, innerErr) should be true")
	}
}

func TestUserError_ErrorsAs_FromWrappedChain(t *testing.T) {
	ue := &cmdutil.UserError{Message: "user-facing"}
	wrapped := fmt.Errorf("context: %w", ue)

	var target *cmdutil.UserError
	if !errors.As(wrapped, &target) {
		t.Error("errors.As should find UserError in chain")
	}
	if target.Message != "user-facing" {
		t.Errorf("target.Message = %q, want %q", target.Message, "user-facing")
	}
}

func TestNewUserError(t *testing.T) {
	ue := cmdutil.NewUserError("bad input")
	if ue.Error() != "bad input" {
		t.Errorf("Error() = %q, want %q", ue.Error(), "bad input")
	}
	if ue.Unwrap() != nil {
		t.Error("Unwrap() should be nil for NewUserError")
	}
}

func TestNewUserErrorf(t *testing.T) {
	ue := cmdutil.NewUserErrorf("value %q is invalid", "foo")
	want := `value "foo" is invalid`
	if ue.Error() != want {
		t.Errorf("Error() = %q, want %q", ue.Error(), want)
	}
}

func TestWrapUserError(t *testing.T) {
	inner := errors.New("underlying issue")
	ue := cmdutil.WrapUserError("friendly message", inner)

	if ue.Error() != "friendly message" {
		t.Errorf("Error() = %q, want %q", ue.Error(), "friendly message")
	}
	if !errors.Is(ue, inner) {
		t.Error("errors.Is should match inner error")
	}
}
