#!/usr/bin/env bash
#
# validate-scripts.sh - Validate PowerShell script equivalence between C# and Go implementations
#
# This script generates PowerShell migration scripts using both the C# and Go
# implementations of the GEI CLI tools, then compares them to ensure they produce
# equivalent outputs.
#
# Usage:
#   ./scripts/validate-scripts.sh [cli-name] [command] [args...]
#
# Examples:
#   ./scripts/validate-scripts.sh gei generate-script --github-source-org test-org
#   ./scripts/validate-scripts.sh ado2gh generate-script --ado-org myorg --github-org myghorg
#   ./scripts/validate-scripts.sh bbs2gh generate-script --bbs-server-url https://bbs.example.com
#
# Exit codes:
#   0 - Scripts are equivalent
#   1 - Scripts differ
#   2 - Usage error or missing dependencies

set -euo pipefail

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Function to print colored output
print_error() {
	echo -e "${RED}ERROR: $1${NC}" >&2
}

print_success() {
	echo -e "${GREEN}SUCCESS: $1${NC}"
}

print_warning() {
	echo -e "${YELLOW}WARNING: $1${NC}"
}

print_info() {
	echo "INFO: $1"
}

# Function to show usage
usage() {
	cat <<EOF
Usage: $0 <cli-name> <command> [args...]

Validate PowerShell script equivalence between C# and Go implementations.

Arguments:
  cli-name    One of: gei, ado2gh, bbs2gh
  command     CLI command (usually 'generate-script')
  args...     Additional arguments to pass to the CLI

Examples:
  $0 gei generate-script --github-source-org test-org
  $0 ado2gh generate-script --ado-org myorg --github-org myghorg
  $0 bbs2gh generate-script --bbs-server-url https://bbs.example.com

Environment Variables:
  SKIP_BUILD     Skip building the binaries (default: false)
  KEEP_TEMP      Keep temporary files after comparison (default: false)
  VERBOSE        Show detailed diff output (default: false)

EOF
	exit 2
}

# Check arguments
if [ $# -lt 2 ]; then
	print_error "Not enough arguments"
	usage
fi

CLI_NAME="$1"
shift
COMMAND="$1"
shift
CLI_ARGS=("$@")

# Validate CLI name
case "$CLI_NAME" in
gei | ado2gh | bbs2gh) ;;
*)
	print_error "Invalid CLI name: $CLI_NAME (must be one of: gei, ado2gh, bbs2gh)"
	usage
	;;
esac

# Check environment variables
SKIP_BUILD="${SKIP_BUILD:-false}"
KEEP_TEMP="${KEEP_TEMP:-false}"
VERBOSE="${VERBOSE:-false}"

# Paths
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CSHARP_PROJECT="$REPO_ROOT/src/$CLI_NAME/$CLI_NAME.csproj"
GO_BINARY="$REPO_ROOT/dist/$CLI_NAME"
TEMP_DIR="$(mktemp -d)"

# Cleanup function
cleanup() {
	if [ "$KEEP_TEMP" != "true" ]; then
		print_info "Cleaning up temporary files..."
		rm -rf "$TEMP_DIR"
	else
		print_info "Temporary files kept in: $TEMP_DIR"
	fi
}
trap cleanup EXIT

# Check dependencies
check_dependencies() {
	local missing=()

	if [ "$SKIP_BUILD" != "true" ]; then
		if ! command -v dotnet &>/dev/null; then
			missing+=("dotnet")
		fi
		if ! command -v go &>/dev/null; then
			missing+=("go")
		fi
	fi

	if ! command -v diff &>/dev/null; then
		missing+=("diff")
	fi

	if [ ${#missing[@]} -gt 0 ]; then
		print_error "Missing required dependencies: ${missing[*]}"
		exit 2
	fi
}

# Build binaries
build_binaries() {
	if [ "$SKIP_BUILD" = "true" ]; then
		print_info "Skipping build (SKIP_BUILD=true)"
		return
	fi

	print_info "Building C# binary..."
	cd "$REPO_ROOT"
	dotnet build "$CSHARP_PROJECT" --configuration Release --output "$TEMP_DIR/csharp" >/dev/null 2>&1

	print_info "Building Go binary..."
	go build -o "$TEMP_DIR/go/$CLI_NAME" "./cmd/$CLI_NAME" >/dev/null 2>&1
}

# Generate script with C# version
generate_csharp_script() {
	local output_file="$1"
	print_info "Generating PowerShell script with C# version..."

	# For generate-script command, it writes to a file (not STDOUT)
	if [ "$COMMAND" = "generate-script" ]; then
		# Create a temp output file path
		local temp_output="$TEMP_DIR/csharp_migrate.ps1"

		# Add --output flag to CLI args
		local args=("${CLI_ARGS[@]}" "--output" "$temp_output")

		# Run the command (output goes to file, not STDOUT)
		dotnet "$TEMP_DIR/csharp/$CLI_NAME.dll" "$COMMAND" "${args[@]}" >/dev/null 2>&1 || {
			print_error "C# script generation failed"
			return 1
		}

		# Copy the generated file to the output location
		cp "$temp_output" "$output_file" || {
			print_error "Failed to copy C# generated script"
			return 1
		}
	else
		# For other commands, output goes to STDOUT
		dotnet "$TEMP_DIR/csharp/$CLI_NAME.dll" "$COMMAND" "${CLI_ARGS[@]}" >"$output_file" 2>/dev/null || {
			print_error "C# script generation failed"
			return 1
		}
	fi
}

# Generate script with Go version
generate_go_script() {
	local output_file="$1"
	print_info "Generating PowerShell script with Go version..."

	# Use the built Go binary (or the one in dist/ if SKIP_BUILD=true)
	local go_bin="$TEMP_DIR/go/$CLI_NAME"
	if [ "$SKIP_BUILD" = "true" ] && [ -f "$GO_BINARY" ]; then
		go_bin="$GO_BINARY"
	fi

	# For generate-script command, it writes to a file (not STDOUT)
	if [ "$COMMAND" = "generate-script" ]; then
		# Create a temp output file path
		local temp_output="$TEMP_DIR/go_migrate.ps1"

		# Add --output flag to CLI args
		local args=("${CLI_ARGS[@]}" "--output" "$temp_output")

		# Run the command (output goes to file, not STDOUT)
		"$go_bin" "$COMMAND" "${args[@]}" >/dev/null 2>&1 || {
			print_error "Go script generation failed"
			return 1
		}

		# Copy the generated file to the output location
		cp "$temp_output" "$output_file" || {
			print_error "Failed to copy Go generated script"
			return 1
		}
	else
		# For other commands, output goes to STDOUT
		"$go_bin" "$COMMAND" "${CLI_ARGS[@]}" >"$output_file" 2>/dev/null || {
			print_error "Go script generation failed"
			return 1
		}
	fi
}

# Normalize script for comparison
# This removes version-specific comments and whitespace differences
normalize_script() {
	local input_file="$1"
	local output_file="$2"

	# Remove version comments, normalize whitespace, remove empty lines at start/end
	grep -v "^# =========== Created with CLI version" "$input_file" |
		grep -v "^# Generated by" |
		grep -v "^# Version:" |
		sed 's/[[:space:]]*$//' |
		sed '/./,$!d' |                                       # Remove leading empty lines
		sed -e :a -e '/^\n*$/{$d;N;ba' -e '}' >"$output_file" # Remove trailing empty lines
}

# Compare scripts
compare_scripts() {
	local csharp_script="$1"
	local go_script="$2"

	print_info "Comparing generated scripts..."

	# Normalize both scripts
	normalize_script "$csharp_script" "$TEMP_DIR/csharp_normalized.ps1"
	normalize_script "$go_script" "$TEMP_DIR/go_normalized.ps1"

	# Compare normalized scripts
	if diff -u "$TEMP_DIR/csharp_normalized.ps1" "$TEMP_DIR/go_normalized.ps1" >"$TEMP_DIR/diff.txt"; then
		print_success "Scripts are equivalent!"
		return 0
	else
		print_error "Scripts differ!"

		if [ "$VERBOSE" = "true" ]; then
			echo ""
			echo "Differences:"
			cat "$TEMP_DIR/diff.txt"
		else
			echo ""
			echo "First 20 lines of differences (set VERBOSE=true for full diff):"
			head -n 20 "$TEMP_DIR/diff.txt"
		fi

		echo ""
		print_info "Full scripts saved to:"
		echo "  C#: $TEMP_DIR/csharp_script.ps1"
		echo "  Go: $TEMP_DIR/go_script.ps1"
		echo "  Diff: $TEMP_DIR/diff.txt"

		return 1
	fi
}

# Main execution
main() {
	print_info "Validating PowerShell script equivalence for '$CLI_NAME $COMMAND'"
	echo ""

	check_dependencies
	build_binaries

	# Generate scripts
	generate_csharp_script "$TEMP_DIR/csharp_script.ps1" || exit 1
	generate_go_script "$TEMP_DIR/go_script.ps1" || exit 1

	# Compare
	if compare_scripts "$TEMP_DIR/csharp_script.ps1" "$TEMP_DIR/go_script.ps1"; then
		exit 0
	else
		exit 1
	fi
}

main
