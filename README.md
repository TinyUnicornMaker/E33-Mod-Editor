# E33 Expedition Infinity Seed Generator

Standalone seed generator for the Clair Obscur: Expedition 33 game. Extracts ~1 GB of game assets on first launch. Builds a pak mod based on the user's settings.


## Build

Requires the **.NET 10 SDK**. Application binaries are published under
`dist/application/<version>/`, separate from the mod release folder
(`dist/Expedition Infinity <version>/`), because the executables trigger
antivirus false positives.

```bash
# Linux x64
CUE4PARSE_SKIP_NATIVE=true dotnet publish src/E33Packer.App/E33Packer.App.csproj \
  -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:IncludeAllContentForSelfExtract=true \
  -p:DebugType=None -p:PublishTrimmed=false \
  -o dist/application/1.4.3/linux

# Windows x64 (run on Windows, or use the GitHub Actions Windows job)
$env:CUE4PARSE_SKIP_NATIVE="true"
dotnet publish src/E33Packer.App/E33Packer.App.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:IncludeAllContentForSelfExtract=true `
  -p:DebugType=None -p:PublishTrimmed=false `
  -o dist/application/1.4.3/windows
```
