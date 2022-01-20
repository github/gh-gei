# Release Notes

- Do not log the error's stack trace to console in non-verberse mode.
- Show a generic error message instead of the actual one for unhandled exceptions in non-verbose mode.
- Exit code is now 1 instead of 0 in case of an error.
- Errors are written to std error instead of std out. 
