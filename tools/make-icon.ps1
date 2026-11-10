# Writes src/ChestTracker/Assets/app.ico (PNG-compressed, 16-256 px) from ChestArt.cs, the same drawing as the overlay.
# Windows only (System.Drawing). Run: pwsh tools/make-icon.ps1, then rebuild to embed it.
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$out = Join-Path $root 'src\ChestTracker\Assets\app.ico'
dotnet run --project (Join-Path $root 'src\ChestTracker') -- --make-icon $out
if ($LASTEXITCODE -ne 0) { throw "make-icon failed ($LASTEXITCODE)" }
Write-Host "wrote $out"
