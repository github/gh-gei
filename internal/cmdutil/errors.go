// Package cmdutil provides shared command infrastructure for the CLI.
package cmdutil

import "fmt"

// UserError is a user-facing error that should be displayed without a stack trace.
// It is the Go equivalent of C#'s OctoshiftCliException.
// Use errors.As to check if an error is a UserError at the top-level handler.
type UserError struct {
	Message string
	Err     error // optional inner error for errors.Unwrap chain
}

func (e *UserError) Error() string { return e.Message }

// Unwrap returns the inner error, enabling errors.Is/errors.As chains.
func (e *UserError) Unwrap() error { return e.Err }

// NewUserError creates a UserError with just a message.
func NewUserError(msg string) *UserError {
	return &UserError{Message: msg}
}

// NewUserErrorf creates a UserError with a formatted message.
func NewUserErrorf(format string, args ...any) *UserError {
	return &UserError{Message: fmt.Sprintf(format, args...)}
}

// WrapUserError wraps an existing error as a UserError with a user-friendly message.
func WrapUserError(msg string, err error) *UserError {
	return &UserError{Message: msg, Err: err}
}
