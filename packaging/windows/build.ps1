<#
.SYNOPSIS
  Builds the DuelLab Windows app: dist\DuelLab\DuelLab.exe and dist\DuelLab-windows.zip.
.DESCRIPTION
  Needs the .NET 10 SDK and Node.js 22. The result is self-contained: friends need nothing installed.
#>
param([string]$Output = "dist")
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path "$PSScriptRoot\..\..").Path
$out = Join-Path $root $Output
$app = Join-Path $out 'DuelLab'
if (Test-Path $out) { Remove-Item -Recurse -Force $out }

Write-Host "Building the web app..."
Push-Location "$root\frontend\duellab-web"
if (-not (Test-Path node_modules)) { npm ci --no-audit --no-fund; if ($LASTEXITCODE) { throw 'npm ci failed' } }
npm run build-only; if ($LASTEXITCODE) { throw 'web build failed' }
Pop-Location

Write-Host "Publishing DuelLab.exe (self-contained, single file)..."
dotnet publish "$root\src\DuelLab.Api" -c Release -r win-x64 --self-contained `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
  -p:DebugType=none -p:GenerateDocumentationFile=false -o $app
if ($LASTEXITCODE) { throw 'dotnet publish failed' }

Rename-Item (Join-Path $app 'DuelLab.Api.exe') 'DuelLab.exe'
Remove-Item (Join-Path $app 'appsettings.Development.json') -ErrorAction SilentlyContinue
Copy-Item -Recurse "$root\frontend\duellab-web\dist" (Join-Path $app 'wwwroot')
Copy-Item "$PSScriptRoot\appsettings.Desktop.json", "$PSScriptRoot\README.txt" $app

Compress-Archive -Path $app -DestinationPath (Join-Path $out 'DuelLab-windows.zip')
Write-Host "Done: $out\DuelLab-windows.zip"
