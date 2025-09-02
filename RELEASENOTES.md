# Release Notes

## What's New
- Added secondary rate limit handling to GitHub CLI commands to gracefully handle 403/429 responses with proper retry logic, exponential backoff, and clear error messaging. This improves reliability when running multiple concurrent migrations.
