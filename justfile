# GitHub Enterprise Importer CLI Build Tasks

# Show available tasks (default)
default:
    @just --list --unsorted

# Variables
solution := "src/OctoshiftCLI.sln"
unit_tests := "src/OctoshiftCLI.Tests/OctoshiftCLI.Tests.csproj"
integration_tests := "src/OctoshiftCLI.IntegrationTests/OctoshiftCLI.IntegrationTests.csproj"

set windows-shell := ["powershell.exe", "-NoLogo", "-Command"]

# Restore all project dependencies
restore:
    dotnet restore {{solution}}

# Build the entire solution
build: restore
    dotnet build {{solution}} --no-restore /p:TreatWarningsAsErrors=true

# Build in release mode
build-release: restore
    dotnet build {{solution}} --no-restore --configuration Release /p:TreatWarningsAsErrors=true

# Format code using dotnet format
format:
    dotnet format {{solution}}

# Verify code formatting (CI check)
format-check:
    dotnet format {{solution}} --verify-no-changes

# Run unit tests
test: build
    dotnet test {{unit_tests}} --no-build --verbosity normal

# Run unit tests with coverage
test-coverage: build
    dotnet test {{unit_tests}} --no-build --verbosity normal --logger:"junit;LogFilePath=unit-tests.xml" --collect:"XPlat Code Coverage" --results-directory ./coverage

# Run integration tests
test-integration: build
    dotnet test {{integration_tests}} --no-build --verbosity normal

# Run all tests (unit + integration)
test-all: build
    dotnet test {{unit_tests}} --no-build --verbosity normal
    dotnet test {{integration_tests}} --no-build --verbosity normal

# Build and run the gei CLI locally
run-gei *args: build
    dotnet run --project src/gei/gei.csproj {{args}}

# Build and run the ado2gh CLI locally
run-ado2gh *args: build
    dotnet run --project src/ado2gh/ado2gh.csproj {{args}}

# Build and run the bbs2gh CLI locally
run-bbs2gh *args: build
    dotnet run --project src/bbs2gh/bbs2gh.csproj {{args}}

# Watch and auto-rebuild gei on changes
watch-gei:
    dotnet watch build --project src/gei/gei.csproj

# Watch and auto-rebuild ado2gh on changes
watch-ado2gh:
    dotnet watch build --project src/ado2gh/ado2gh.csproj

# Watch and auto-rebuild bbs2gh on changes
watch-bbs2gh:
    dotnet watch build --project src/bbs2gh/bbs2gh.csproj

# Build self-contained binaries for all platforms (requires PowerShell)
publish:
    pwsh ./publish.ps1

# Build only Linux binaries 
publish-linux:
    #!/usr/bin/env pwsh
    $env:SKIP_WINDOWS = "true"
    $env:SKIP_MACOS = "true"
    ./publish.ps1

# Build only Windows binaries
publish-windows:
    #!/usr/bin/env pwsh
    $env:SKIP_LINUX = "true"
    $env:SKIP_MACOS = "true"
    ./publish.ps1

# Build only macOS binaries
publish-macos:
    #!/usr/bin/env pwsh
    $env:SKIP_WINDOWS = "true"
    $env:SKIP_LINUX = "true"
    ./publish.ps1

# Clean build artifacts
clean:
    dotnet clean {{solution}}
    rm -rf dist/
    rm -rf coverage/

# Full CI pipeline: format check, build, and test
ci: format-check build test

# Full development workflow: format, build, test
dev: format build test

# Install gh CLI extensions locally (requires built binaries)
install-extensions: publish-linux
    #!/usr/bin/env bash
    set -euo pipefail
    
    # Create extension directories
    mkdir -p gh-gei gh-ado2gh gh-bbs2gh
    
    # Copy binaries
    cp ./dist/linux-x64/gei-linux-amd64 ./gh-gei/gh-gei
    cp ./dist/linux-x64/ado2gh-linux-amd64 ./gh-ado2gh/gh-ado2gh
    cp ./dist/linux-x64/bbs2gh-linux-amd64 ./gh-bbs2gh/gh-bbs2gh
    
    # Set execute permissions
    chmod +x ./gh-gei/gh-gei ./gh-ado2gh/gh-ado2gh ./gh-bbs2gh/gh-bbs2gh
    
    # Install extensions
    cd gh-gei && gh extension install . && cd ..
    cd gh-ado2gh && gh extension install . && cd ..
    cd gh-bbs2gh && gh extension install . && cd ..
    
    echo "Extensions installed successfully!"
