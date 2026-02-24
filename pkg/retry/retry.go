package retry

import (
	"context"
	"fmt"
	"time"

	"github.com/avast/retry-go/v4"
)

// Policy provides retry capabilities for operations
// Equivalent to C# RetryPolicy using Polly
type Policy struct {
	maxAttempts uint
	delay       time.Duration
	maxDelay    time.Duration
}

// Option is a functional option for configuring the retry policy
type Option func(*Policy)

// WithMaxAttempts sets the maximum number of retry attempts
func WithMaxAttempts(attempts uint) Option {
	return func(p *Policy) {
		p.maxAttempts = attempts
	}
}

// WithDelay sets the initial delay between retries
func WithDelay(delay time.Duration) Option {
	return func(p *Policy) {
		p.delay = delay
	}
}

// WithMaxDelay sets the maximum delay between retries
func WithMaxDelay(maxDelay time.Duration) Option {
	return func(p *Policy) {
		p.maxDelay = maxDelay
	}
}

// New creates a new retry policy with the given options
func New(opts ...Option) *Policy {
	p := &Policy{
		maxAttempts: 3,
		delay:       1 * time.Second,
		maxDelay:    30 * time.Second,
	}

	for _, opt := range opts {
		opt(p)
	}

	return p
}

// Execute executes the given function with retry logic
func (p *Policy) Execute(ctx context.Context, fn func() error) error {
	return retry.Do(
		fn,
		retry.Attempts(p.maxAttempts),
		retry.Delay(p.delay),
		retry.MaxDelay(p.maxDelay),
		retry.Context(ctx),
		retry.DelayType(retry.BackOffDelay),
		retry.OnRetry(func(n uint, err error) {
			// Could add logging here if needed
		}),
	)
}

// ExecuteWithResult executes the given function with retry logic and returns a result
func ExecuteWithResult[T any](ctx context.Context, p *Policy, fn func() (T, error)) (T, error) {
	var result T
	var lastErr error

	err := p.Execute(ctx, func() error {
		var err error
		result, err = fn()
		lastErr = err
		return err
	})

	if err != nil {
		return result, fmt.Errorf("retry failed after %d attempts: %w", p.maxAttempts, lastErr)
	}

	return result, nil
}

// HTTPRetryableStatusCodes returns HTTP status codes that should trigger a retry
func HTTPRetryableStatusCodes() []int {
	return []int{408, 429, 500, 502, 503, 504}
}

// IsRetryableHTTPStatus checks if an HTTP status code should trigger a retry
func IsRetryableHTTPStatus(statusCode int) bool {
	for _, code := range HTTPRetryableStatusCodes() {
		if statusCode == code {
			return true
		}
	}
	return false
}
