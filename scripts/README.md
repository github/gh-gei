# Script Validation

This directory contains tools for validating that the Go port of GEI produces equivalent PowerShell scripts to the C# version.

## validate-scripts.sh

Automated validation tool that:
1. Builds both C# and Go versions of a CLI
2. Runs `generate-script` with identical arguments
3. Normalizes outputs (removes version comments, whitespace)
4. Compares scripts for equivalence

### Usage

```bash
# Basic usage
./scripts/validate-scripts.sh gei generate-script --github-source-org test-org

# With environment variables
VERBOSE=true ./scripts/validate-scripts.sh ado2gh generate-script \
    --ado-org myorg \
    --github-org myghorg \
    --download-migration-logs

# Skip rebuilding (use existing binaries)
SKIP_BUILD=true ./scripts/validate-scripts.sh bbs2gh generate-script \
    --bbs-server-url https://bbs.example.com \
    --github-org target-org
```

### Environment Variables

- `SKIP_BUILD` - Skip building binaries (uses existing in `dist/`)
- `KEEP_TEMP` - Keep temporary files after comparison
- `VERBOSE` - Show full diff output

### Exit Codes

- `0` - Scripts are equivalent
- `1` - Scripts differ
- `2` - Usage error or missing dependencies

## CI Integration

The validation script will be integrated into the CI workflow to automatically validate script equivalence on every PR that touches the Go implementation.

See `.github/workflows/validate-scripts.yml` (to be created in Phase 3).
