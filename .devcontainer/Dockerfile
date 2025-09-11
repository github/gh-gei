# Start from the .NET 8 devcontainer image
FROM mcr.microsoft.com/devcontainers/dotnet:9.0

# Set up apt and install Just as a task runner
RUN apt-get update \
    && apt-get install -y curl ca-certificates gnupg \
    && curl --proto '=https' --tlsv1.2 -sSf https://just.systems/install.sh | bash -s -- --to /usr/local/bin \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*
