# Build Script for Shelly Loupedeck Plugin
# Run this script to build and optionally install the plugin

param(
    [switch]$Install,
    [switch]$Debug,
    [switch]$Clean
)

$ErrorActionPreference = "Stop"

Write-Host "=== Shelly Loupedeck Plugin Build Script ===" -ForegroundColor Cyan

# Clean previous builds
if ($Clean) {
    Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
    if (Test-Path "bin") { Remove-Item -Recurse -Force "bin" }
    if (Test-Path "obj") { Remove-Item -Recurse -Force "obj" }
}

# Determine build configuration
$config = if ($Debug) { "Debug" } else { "Release" }
Write-Host "Building in $config mode..." -ForegroundColor Yellow

# Restore NuGet packages
Write-Host "Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore

if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to restore packages!" -ForegroundColor Red
    exit 1
}

# Build the project
Write-Host "Building project..." -ForegroundColor Yellow
dotnet build -c $config

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "Build successful!" -ForegroundColor Green

# Install to Loupedeck directory
if ($Install) {
    Write-Host "Installing plugin to Loupedeck..." -ForegroundColor Yellow

    $loupedeckPlugins = "$env:LOCALAPPDATA\Loupedeck\Plugins\ShellyLoupedeckPlugin"
    $buildOutput = "bin\$config\net8.0-windows"

    # Check if Loupedeck is running
    $loupedeckRunning = Get-Process "Loupedeck" -ErrorAction SilentlyContinue
    if ($loupedeckRunning) {
        Write-Host "WARNING: Loupedeck is currently running!" -ForegroundColor Red
        Write-Host "Please close Loupedeck before installing the plugin." -ForegroundColor Yellow
        $response = Read-Host "Continue anyway? (y/N)"
        if ($response -ne "y") {
            Write-Host "Installation cancelled." -ForegroundColor Yellow
            exit 0
        }
    }

    # Create plugin directory
    if (-not (Test-Path $loupedeckPlugins)) {
        New-Item -ItemType Directory -Path $loupedeckPlugins | Out-Null
    }

    # Copy files
    Write-Host "Copying files to $loupedeckPlugins..." -ForegroundColor Yellow
    Copy-Item -Path "$buildOutput\*" -Destination $loupedeckPlugins -Recurse -Force

    Write-Host "Plugin installed successfully!" -ForegroundColor Green
    Write-Host "You can now start Loupedeck." -ForegroundColor Cyan
}

Write-Host ""
Write-Host "=== Build Complete ===" -ForegroundColor Cyan
Write-Host "Output directory: bin\$config\net8.0-windows" -ForegroundColor Gray

if (-not $Install) {
    Write-Host ""
    Write-Host "To install the plugin, run:" -ForegroundColor Yellow
    Write-Host "  .\build.ps1 -Install" -ForegroundColor White
}
