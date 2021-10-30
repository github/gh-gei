$AssemblyVersion = "0.0"

if ((Test-Path env:CLI_VERSION) -And $env:CLI_VERSION.StartsWith("refs/tags/v"))
{
    $AssemblyVersion = $env:CLI_VERSION.Substring(11)
}

Write-Output "version: $AssemblyVersion"

dotnet publish src/OctoshiftCLI/OctoshiftCLI.csproj -c Release -o dist/win-x64/ -r win-x64 -p:PublishSingleFile=true -p:PublishTrimmed=true --self-contained true /p:DebugType=None /p:IncludeNativeLibrariesForSelfExtract=true /p:VersionPrefix=$AssemblyVersion

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

dotnet publish src/OctoshiftCLI/OctoshiftCLI.csproj -c Release -o dist/linux-x64/ -r linux-x64 -p:PublishSingleFile=true -p:PublishTrimmed=true --self-contained true /p:DebugType=None /p:IncludeNativeLibrariesForSelfExtract=true /p:VersionPrefix=$AssemblyVersion

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

dotnet publish src/OctoshiftCLI/OctoshiftCLI.csproj -c Release -o dist/osx-x64/ -r osx-x64 -p:PublishSingleFile=true -p:PublishTrimmed=true --self-contained true /p:DebugType=None /p:IncludeNativeLibrariesForSelfExtract=true /p:VersionPrefix=$AssemblyVersion

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Compress-Archive -Path ./dist/win-x64/octoshift.exe -DestinationPath ./dist/octoshift.win-x64.zip -Force
tar -cvzf ./dist/octoshift.linux-x64.tar.gz -C ./dist/linux-x64 octoshift
tar -cvzf ./dist/octoshift.osx-x64.tar.gz -C ./dist/osx-x64 octoshift