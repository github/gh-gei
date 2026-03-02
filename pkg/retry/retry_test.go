package retry_test

import (
	"context"
	"errors"
	"testing"
	"time"

	"github.com/github/gh-gei/pkg/retry"
)

func TestRetry_Execute_Success(t *testing.T) {
	policy := retry.New()
	ctx := context.Background()

	callCount := 0
	err := policy.Execute(ctx, func() error {
		callCount++
		return nil
	})

	if err != nil {
		t.Errorf("expected no error, got: %v", err)
	}
	if callCount != 1 {
		t.Errorf("expected 1 call, got: %d", callCount)
	}
}

func TestRetry_Execute_SuccessAfterRetry(t *testing.T) {
	policy := retry.New(retry.WithMaxAttempts(3), retry.WithDelay(10*time.Millisecond))
	ctx := context.Background()

	callCount := 0
	err := policy.Execute(ctx, func() error {
		callCount++
		if callCount < 2 {
			return errors.New("temporary error")
		}
		return nil
	})

	if err != nil {
		t.Errorf("expected no error after retry, got: %v", err)
	}
	if callCount != 2 {
		t.Errorf("expected 2 calls, got: %d", callCount)
	}
}

func TestRetry_Execute_MaxAttemptsExceeded(t *testing.T) {
	policy := retry.New(retry.WithMaxAttempts(3), retry.WithDelay(10*time.Millisecond))
	ctx := context.Background()

	callCount := 0
	testErr := errors.New("persistent error")

	err := policy.Execute(ctx, func() error {
		callCount++
		return testErr
	})

	if err == nil {
		t.Error("expected error after max attempts, got nil")
	}
	if callCount != 3 {
		t.Errorf("expected 3 calls, got: %d", callCount)
	}
}

func TestRetry_ExecuteWithResult(t *testing.T) {
	policy := retry.New(retry.WithDelay(10 * time.Millisecond))
	ctx := context.Background()

	callCount := 0
	result, err := retry.ExecuteWithResult(ctx, policy, func() (string, error) {
		callCount++
		if callCount < 2 {
			return "", errors.New("temporary error")
		}
		return "success", nil
	})

	if err != nil {
		t.Errorf("expected no error, got: %v", err)
	}
	if result != "success" {
		t.Errorf("expected 'success', got: %s", result)
	}
	if callCount != 2 {
		t.Errorf("expected 2 calls, got: %d", callCount)
	}
}

func TestRetry_ContextCancellation(t *testing.T) {
	policy := retry.New(retry.WithMaxAttempts(10), retry.WithDelay(100*time.Millisecond))
	ctx, cancel := context.WithTimeout(context.Background(), 50*time.Millisecond)
	defer cancel()

	err := policy.Execute(ctx, func() error {
		return errors.New("error")
	})

	if err == nil {
		t.Error("expected context cancellation error")
	}
}

func TestRetry_IsRetryableHTTPStatus(t *testing.T) {
	tests := []struct {
		statusCode int
		want       bool
	}{
		{200, false},
		{201, false},
		{400, false},
		{401, false},
		{403, false},
		{404, false},
		{408, true}, // Request Timeout
		{429, true}, // Too Many Requests
		{500, true}, // Internal Server Error
		{502, true}, // Bad Gateway
		{503, true}, // Service Unavailable
		{504, true}, // Gateway Timeout
	}

	for _, tt := range tests {
		t.Run(string(rune(tt.statusCode)), func(t *testing.T) {
			got := retry.IsRetryableHTTPStatus(tt.statusCode)
			if got != tt.want {
				t.Errorf("IsRetryableHTTPStatus(%d) = %v, want %v", tt.statusCode, got, tt.want)
			}
		})
	}
}

func TestRetry_Options(t *testing.T) {
	policy := retry.New(
		retry.WithMaxAttempts(5),
		retry.WithDelay(2*time.Second),
		retry.WithMaxDelay(60*time.Second),
	)

	// We can't directly test private fields, but we can test the behavior
	ctx := context.Background()
	callCount := 0

	err := policy.Execute(ctx, func() error {
		callCount++
		if callCount < 5 {
			return errors.New("error")
		}
		return nil
	})

	if err != nil {
		t.Errorf("expected success after retries, got: %v", err)
	}
	if callCount != 5 {
		t.Errorf("expected 5 calls, got: %d", callCount)
	}
}
