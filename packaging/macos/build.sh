#!/usr/bin/env bash
# Builds the DuelLab macOS app: dist/DuelLab/DuelLab and dist/DuelLab-macos-<arch>.zip.
#
# Usage: packaging/macos/build.sh [arm64|x64] [output-dir]
# Run it on a Mac (Apple Silicon only runs signed programs, and signing needs macOS).
# Needs the .NET 10 SDK and Node.js 22. The result is self-contained: nothing to install.
set -euo pipefail

arch="${1:-arm64}"
case "$arch" in arm64|x64) ;; *) echo "arch must be arm64 or x64" >&2; exit 1 ;; esac
root="$(cd "$(dirname "$0")/../.." && pwd)"
out="$root/${2:-dist}"
app="$out/DuelLab"
rm -rf "$out"

echo "Building the web app..."
(cd "$root/frontend/duellab-web" && { [ -d node_modules ] || npm ci --no-audit --no-fund; } && npm run build-only)

echo "Publishing DuelLab ($arch, self-contained, single file)..."
dotnet publish "$root/src/DuelLab.Api" -c Release -r "osx-$arch" --self-contained \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true \
  -p:DebugType=none -p:GenerateDocumentationFile=false -o "$app"

mv "$app/DuelLab.Api" "$app/DuelLab"
rm -f "$app/appsettings.Development.json"
cp -R "$root/frontend/duellab-web/dist" "$app/wwwroot"
cp "$root/packaging/desktop/appsettings.Desktop.json" "$app/"
cp "$root/packaging/macos/README.txt" "$app/"
chmod +x "$app/DuelLab"

# Apple Silicon refuses to run unsigned programs; an ad-hoc signature is enough to run it locally.
codesign --force --sign - "$app/DuelLab"

# ditto keeps the executable permission inside the zip.
ditto -c -k --keepParent "$app" "$out/DuelLab-macos-$arch.zip"
echo "Done: $out/DuelLab-macos-$arch.zip"
