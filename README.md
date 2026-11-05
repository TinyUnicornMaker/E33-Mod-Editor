# E33 Mod Editor

Standalone editor for Clair Obscur: Expedition 33 game data, starting with
monster DataTable editing. Built for Linux and Windows.

## Build

Requires the **.NET 10 SDK**.

```bash
# Linux x64
CUE4PARSE_SKIP_NATIVE=true dotnet publish src/E33Packer.App/E33Packer.App.csproj \
  -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:IncludeAllContentForSelfExtract=true \
  -p:DebugType=None -p:PublishTrimmed=false \
  -o dist/linux-x64

# Windows x64 (run on Windows, or use the GitHub Actions Windows job)
$env:CUE4PARSE_SKIP_NATIVE="true"
dotnet publish src/E33Packer.App/E33Packer.App.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:IncludeAllContentForSelfExtract=true `
  -p:DebugType=None -p:PublishTrimmed=false `
  -o dist/win-x64
```
