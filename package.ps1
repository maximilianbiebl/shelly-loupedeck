# Build and Package Script for Shelly Loupedeck Plugin
# Creates a .lplug4 package that can be installed in Loupedeck

param(
    [switch]$Clean
)

$ErrorActionPreference = "Stop"

Write-Host "=== Shelly Loupedeck Plugin - Build & Package ===" -ForegroundColor Cyan

# Clean previous builds
if ($Clean) {
    Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
    if (Test-Path "bin") { Remove-Item -Recurse -Force "bin" }
    if (Test-Path "obj") { Remove-Item -Recurse -Force "obj" }
    if (Test-Path "package") { Remove-Item -Recurse -Force "package" }
    if (Test-Path "*.lplug4") { Remove-Item -Force "*.lplug4" }
}

# Build Release
Write-Host "Building Release..." -ForegroundColor Yellow
dotnet restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "Restore failed!" -ForegroundColor Red
    exit 1
}

dotnet build -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "Build successful!" -ForegroundColor Green

# Create package directory structure
Write-Host "Creating package structure..." -ForegroundColor Yellow
$packageDir = "package"
$winDir = "$packageDir\win"

if (Test-Path $packageDir) { Remove-Item -Recurse -Force $packageDir }
New-Item -ItemType Directory -Path $winDir | Out-Null
New-Item -ItemType Directory -Path "$packageDir\metadata" | Out-Null

# Copy files
$buildOutput = "bin\Release\net8.0-windows"

Write-Host "Copying plugin files..." -ForegroundColor Yellow

# Copy DLLs (excluding PluginApi.dll which comes from Loupedeck)
Copy-Item "$buildOutput\ShellyLoupedeckPlugin.dll" -Destination $winDir
Copy-Item "$buildOutput\ShellyLoupedeckPlugin.pdb" -Destination $winDir -ErrorAction SilentlyContinue
Copy-Item "$buildOutput\Newtonsoft.Json.dll" -Destination $winDir

# Copy manifest and metadata
Copy-Item "LoupedeckPackage.yaml" -Destination $packageDir
Copy-Item "metadata\Icon16x16.png" -Destination "$packageDir\metadata"
Copy-Item "metadata\Icon256x256.png" -Destination "$packageDir\metadata"

# Create .lplug4 package (ZIP file)
Write-Host "Creating .lplug4 package..." -ForegroundColor Yellow
$pluginName = "ShellyCloudControl.lplug4"

if (Test-Path $pluginName) { Remove-Item -Force $pluginName }

# Zip the package directory
Compress-Archive -Path "$packageDir\*" -DestinationPath "temp.zip"
Rename-Item "temp.zip" $pluginName

# Clean up
Remove-Item -Recurse -Force $packageDir

Write-Host ""
Write-Host "=== Package Created Successfully! ===" -ForegroundColor Green
Write-Host "Package: $pluginName" -ForegroundColor Cyan
Write-Host ""
Write-Host "Installation:" -ForegroundColor Yellow
Write-Host "1. Close Loupedeck Software" -ForegroundColor White
Write-Host "2. Double-click on $pluginName" -ForegroundColor White
Write-Host "   OR drag & drop it into Loupedeck Software" -ForegroundColor White
Write-Host "3. Restart Loupedeck Software" -ForegroundColor White
Write-Host ""
