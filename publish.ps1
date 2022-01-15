$AssemblyVersion = "0.0"

if ((Test-Path env:CLI_VERSION) -And $env:CLI_VERSION.StartsWith("refs/tags/v"))
{
    $AssemblyVersion = $env:CLI_VERSION.Substring(11)
}

Write-Output "version: $AssemblyVersion"

### ado2gh ###
dotnet publish src/ado2gh/ado2gh.csproj -c Release -o dist/win-x64/ -r win-x64 -p:PublishSingleFile=true -p:PublishTrimmed=true --self-contained true /p:DebugType=None /p:IncludeNativeLibrariesForSelfExtract=true /p:VersionPrefix=$AssemblyVersion

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

dotnet publish src/ado2gh/ado2gh.csproj -c Release -o dist/linux-x64/ -r linux-x64 -p:PublishSingleFile=true -p:PublishTrimmed=true --self-contained true /p:DebugType=None /p:IncludeNativeLibrariesForSelfExtract=true /p:VersionPrefix=$AssemblyVersion

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

dotnet publish src/ado2gh/ado2gh.csproj -c Release -o dist/osx-x64/ -r osx-x64 -p:PublishSingleFile=true -p:PublishTrimmed=true --self-contained true /p:DebugType=None /p:IncludeNativeLibrariesForSelfExtract=true /p:VersionPrefix=$AssemblyVersion

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Compress-Archive -Path ./dist/win-x64/ado2gh.exe -DestinationPath ./dist/ado2gh.$AssemblyVersion.win-x64.zip -Force
tar -cvzf ./dist/ado2gh.$AssemblyVersion.linux-x64.tar.gz -C ./dist/linux-x64 ado2gh
tar -cvzf ./dist/ado2gh.$AssemblyVersion.osx-x64.tar.gz -C ./dist/osx-x64 ado2gh

### gei ###
dotnet publish src/gei/gei.csproj -c Release -o dist/win-x64/ -r win-x64 -p:PublishSingleFile=true -p:PublishTrimmed=true --self-contained true /p:DebugType=None /p:IncludeNativeLibrariesForSelfExtract=true /p:VersionPrefix=$AssemblyVersion

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

dotnet publish src/gei/gei.csproj -c Release -o dist/linux-x64/ -r linux-x64 -p:PublishSingleFile=true -p:PublishTrimmed=true --self-contained true /p:DebugType=None /p:IncludeNativeLibrariesForSelfExtract=true /p:VersionPrefix=$AssemblyVersion

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

dotnet publish src/gei/gei.csproj -c Release -o dist/osx-x64/ -r osx-x64 -p:PublishSingleFile=true -p:PublishTrimmed=true --self-contained true /p:DebugType=None /p:IncludeNativeLibrariesForSelfExtract=true /p:VersionPrefix=$AssemblyVersion

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Rename-Item ./dist/win-x64/gei.exe gei-windows-amd64.exe
Rename-Item ./dist/linux-x64/gei gei-linux-amd64
Rename-Item ./dist/osx-x64/gei gei-darwin-amd64