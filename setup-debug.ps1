# Setup script to configure debug environment for JukeboxSpotify

# This script helps configure Visual Studio debugging for the Subnautica mod.
# Run this once to set up your environment.

$subnauticaPath = "C:\Program Files (x86)\Steam\steamapps\common\Subnautica"  # Adjust if needed
$modName = "JukeboxSpotify"

# Check if Subnautica is installed
if (-not (Test-Path $subnauticaPath)) {
    Write-Host "Subnautica path not found at: $subnauticaPath"
    Write-Host "Please update the path in this script to match your installation."
    exit 1
}

$bepinexPath = "$subnauticaPath\BepInEx\plugins\$modName"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "JukeboxSpotify Debug Setup" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Subnautica Install: $subnauticaPath"
Write-Host "Target Plugin Path: $bepinexPath"
Write-Host ""

# Create plugin directory if it doesn't exist
if (-not (Test-Path $bepinexPath)) {
    Write-Host "Creating plugin directory..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $bepinexPath -Force | Out-Null
}

Write-Host "Setup complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Build your project (Ctrl+Shift+B)"
Write-Host "2. Start Subnautica"
Write-Host "3. In Visual Studio: Debug → Attach to Process (Ctrl+Alt+P)"
Write-Host "4. Find and select subnautica.exe"
Write-Host "5. View logs in: View → Output Window (Ctrl+Alt+O)"
Write-Host "6. In the Output dropdown, select 'Debug'"
Write-Host ""
Write-Host "Alternative: Press F12 in-game for BepInEx console"
Write-Host ""
