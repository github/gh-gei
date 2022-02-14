$AssemblyVersion = "0.0"

if ((Test-Path env:CLI_VERSION) -And $env:CLI_VERSION.StartsWith("refs/tags/v")) {
    $AssemblyVersion = $env:CLI_VERSION.Substring(11)
}

Write-Output "version: $AssemblyVersion"

### ado2gh ###
if ((Test-Path env:SKIP_WINDOWS) -And $env:SKIP_WINDOWS.ToUpper() -eq "TRUE") {
    Write-Output "Skipping ado2gh Windows build because SKIP_WINDOWS is set"
}
else {
    dotnet publish src/ado2gh/ado2gh.csproj -c Release -o dist/win-x64/ -r win-x64 -p:PublishSingleFile=true -p:PublishTrimmed=true --self-contained true /p:DebugType=None /p:IncludeNativeLibrariesForSelfExtract=true /p:VersionPrefix=$AssemblyVersion

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    Compress-Archive -Path ./dist/win-x64/ado2gh.exe -DestinationPath ./dist/ado2gh.$AssemblyVersion.win-x64.zip -Force
}

if ((Test-Path env:SKIP_LINUX) -And $env:SKIP_LINUX.ToUpper() -eq "TRUE") {
    Write-Output "Skipping ado2gh Linux build because SKIP_LINUX is set"
}
else {
    dotnet publish src/ado2gh/ado2gh.csproj -c Release -o dist/linux-x64/ -r linux-x64 -p:PublishSingleFile=true -p:PublishTrimmed=true --self-contained true /p:DebugType=None /p:IncludeNativeLibrariesForSelfExtract=true /p:VersionPrefix=$AssemblyVersion

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    tar -cvzf ./dist/ado2gh.$AssemblyVersion.linux-x64.tar.gz -C ./dist/linux-x64 ado2gh
}

if ((Test-Path env:SKIP_MACOS) -And $env:SKIP_MACOS.ToUpper() -eq "TRUE") {
    Write-Output "Skipping ado2gh MacOS build because SKIP_MACOS is set"
}
else {
    dotnet publish src/ado2gh/ado2gh.csproj -c Release -o dist/osx-x64/ -r osx-x64 -p:PublishSingleFile=true -p:PublishTrimmed=true --self-contained true /p:DebugType=None /p:IncludeNativeLibrariesForSelfExtract=true /p:VersionPrefix=$AssemblyVersion

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    tar -cvzf ./dist/ado2gh.$AssemblyVersion.osx-x64.tar.gz -C ./dist/osx-x64 ado2gh
}  


### gei ###
if ((Test-Path env:SKIP_WINDOWS) -And $env:SKIP_WINDOWS.ToUpper() -eq "TRUE") {
    Write-Output "Skipping gei Windows build because SKIP_WINDOWS is set"
}
else {
    dotnet publish src/gei/gei.csproj -c Release -o dist/win-x64/ -r win-x64 -p:PublishSingleFile=true -p:PublishTrimmed=true --self-contained true /p:DebugType=None /p:IncludeNativeLibrariesForSelfExtract=true /p:VersionPrefix=$AssemblyVersion

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    if (Test-Path -Path ./dist/win-x64/gei-windows-amd64.exe) {
        Remove-Item ./dist/win-x64/gei-windows-amd64.exe
    }

    Rename-Item ./dist/win-x64/gei.exe gei-windows-amd64.exe
}

if ((Test-Path env:SKIP_LINUX) -And $env:SKIP_LINUX.ToUpper() -eq "TRUE") {
    Write-Output "Skipping gei Linux build because SKIP_LINUX is set"
}
else {
    dotnet publish src/gei/gei.csproj -c Release -o dist/linux-x64/ -r linux-x64 -p:PublishSingleFile=true -p:PublishTrimmed=true --self-contained true /p:DebugType=None /p:IncludeNativeLibrariesForSelfExtract=true /p:VersionPrefix=$AssemblyVersion

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    if (Test-Path -Path ./dist/linux-x64/gei-linux-amd64) {
        Remove-Item ./dist/linux-x64/gei-linux-amd64
    }

    Rename-Item ./dist/linux-x64/gei gei-linux-amd64
}

if ((Test-Path env:SKIP_MACOS) -And $env:SKIP_MACOS.ToUpper() -eq "TRUE") {
    Write-Output "Skipping gei MacOS build because SKIP_MACOS is set"
}
else {
    dotnet publish src/gei/gei.csproj -c Release -o dist/osx-x64/ -r osx-x64 -p:PublishSingleFile=true -p:PublishTrimmed=true --self-contained true /p:DebugType=None /p:IncludeNativeLibrariesForSelfExtract=true /p:VersionPrefix=$AssemblyVersion

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    if (Test-Path -Path ./dist/osx-x64/gei-darwin-amd64) {
        Remove-Item ./dist/osx-x64/gei-darwin-amd64
    }

    Rename-Item ./dist/osx-x64/gei gei-darwin-amd64
}







