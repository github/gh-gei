# Getting Started Contributing

Familiarize yourself with [.NET core](https://docs.microsoft.com/en-us/dotnet/core/introduction#net-core-and-net-5), which is a cross-platform framework for .NET (also read "dotnet").

## Basic Getting Started

Try developing this repo using GitHub codespaces, so all the dependencies are installed for you!

Check out `publish.ps1` to see how binaries are built for this repo for various distributions.

Build with:
```bash
dotnet build src/OctoshiftCLI.sln
```

Alternatively, you can use for gei
```bash
 dotnet watch build --project  src/gei/gei.csproj
```
to run builds automatically.
- If you're doing this, you can run the binaries with `./src/gei/bin/Debug/net6.0/gei`

If you aren't using watch, run **after** building with:
```bash
dotnet run --project src/gei/gei.csproj
```

Run tests with
```bash
dotnet test src/OctoshiftCLI.Tests/OctoshiftCLI.Tests.csproj
```

Format your files locally with:
```bash
dotnet format src/OctoshiftCLI.sln
```