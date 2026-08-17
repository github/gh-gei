package cmdutil

import (
	"net/url"
	"strings"
)

// IsURL checks if a string is a valid HTTP or HTTPS URL.
func IsURL(s string) bool {
	if s == "" {
		return false
	}
	u, err := url.ParseRequestURI(s)
	if err != nil {
		return false
	}
	return u.Scheme == "http" || u.Scheme == "https"
}

// ValidateNoURL returns a UserError if the value looks like a URL.
// flagName is the flag name for the error message (e.g. "--github-org").
func ValidateNoURL(value, flagName string) error {
	if IsURL(value) {
		return NewUserErrorf("%s expects a name, not a URL. Remove the URL and provide just the name.", flagName)
	}
	return nil
}

// ValidateRequired returns a UserError if the value is empty or whitespace-only.
func ValidateRequired(value, flagName string) error {
	if strings.TrimSpace(value) == "" {
		return NewUserErrorf("%s must be provided", flagName)
	}
	return nil
}

// ValidateMutuallyExclusive returns a UserError if both values are non-empty.
func ValidateMutuallyExclusive(val1, flag1, val2, flag2 string) error {
	if val1 != "" && val2 != "" {
		return NewUserErrorf("only one of %s or %s can be set at a time", flag1, flag2)
	}
	return nil
}

// ValidatePaired returns a UserError if exactly one of the two values is set.
// Both must be provided together or neither.
func ValidatePaired(val1, flag1, val2, flag2 string) error {
	set1 := val1 != ""
	set2 := val2 != ""
	if set1 != set2 {
		return NewUserErrorf("%s and %s must be provided together", flag1, flag2)
	}
	return nil
}

// ValidateRequiredWhen returns a UserError if condition is true but value is empty.
func ValidateRequiredWhen(value, flagName string, condition bool, conditionDesc string) error {
	if condition && strings.TrimSpace(value) == "" {
		return NewUserErrorf("%s must be specified when %s", flagName, conditionDesc)
	}
	return nil
}

// ValidateOneOf returns a UserError if value is not in the allowed list (case-insensitive).
// An empty value is considered valid (flag not set).
func ValidateOneOf(value, flagName string, allowed ...string) error {
	if value == "" {
		return nil
	}
	upper := strings.ToUpper(value)
	for _, a := range allowed {
		if strings.ToUpper(a) == upper {
			return nil
		}
	}
	return NewUserErrorf("%s must be one of: %s", flagName, strings.Join(allowed, ", "))
}
