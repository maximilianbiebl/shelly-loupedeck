# Build and Package Script for Shelly Loupedeck Plugin
# Creates a .lplug4 package that can be installed in Loupedeck

param(
    [switch]$Clean,
    [string]$Version
)

$ErrorActionPreference = "Stop"

Write-Host "=== Shelly Loupedeck Plugin - Build & Package ===" -ForegroundColor Cyan

# Resolve the package version.
# Loupedeck keeps the already-installed plugin when a package does not raise its
# version, so shipping 1.0.0 every time silently makes updates no-ops. Bump the
# patch number on every build unless an explicit -Version is given.
$manifestPath = "LoupedeckPackage.yaml"
$manifest = Get-Content $manifestPath -Raw

if (-not $Version) {
    if ($manifest -match '(?m)^version:\s*(\d+)\.(\d+)\.(\d+)\s*$') {
        $Version = "$($Matches[1]).$($Matches[2]).$([int]$Matches[3] + 1)"
    }
    else {
        Write-Host "Could not read 'version:' from $manifestPath" -ForegroundColor Red
        exit 1
    }
}

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    Write-Host "Version must look like MAJOR.MINOR.PATCH (got '$Version')" -ForegroundColor Red
    exit 1
}

# Persist the new version so the next build increments from here
$manifest = $manifest -replace '(?m)^version:\s*\d+\.\d+\.\d+\s*$', "version: $Version"
Set-Content $manifestPath -Value $manifest -NoNewline

Write-Host "Package version: $Version" -ForegroundColor Cyan

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

# Stamp the version into the assembly so the running build is identifiable at
# runtime (the plugin logs it on load)
dotnet build -c Release -p:Platform=x64 `
    -p:Version=$Version `
    -p:AssemblyVersion="$Version.0" `
    -p:FileVersion="$Version.0"
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
$buildOutput = "bin\x64\Release\net472"

Write-Host "Copying plugin files..." -ForegroundColor Yellow

# Copy all DLLs (excluding PluginApi.dll which comes from Loupedeck)
Get-ChildItem "$buildOutput\*.dll" | Where-Object { $_.Name -ne "PluginApi.dll" } | ForEach-Object {
    Write-Host "  Copying $($_.Name)..." -ForegroundColor Gray
    Copy-Item $_.FullName -Destination $winDir
}

# Copy PDB files for debugging
Get-ChildItem "$buildOutput\*.pdb" -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "  Copying $($_.Name)..." -ForegroundColor Gray
    Copy-Item $_.FullName -Destination $winDir
}

# Copy manifest and metadata
Copy-Item "LoupedeckPackage.yaml" -Destination $packageDir
Copy-Item "metadata\Icon16x16.png" -Destination "$packageDir\metadata"
Copy-Item "metadata\Icon256x256.png" -Destination "$packageDir\metadata"

# Copy configuration files to PLUGIN ROOT directory (not win subdirectory)
if (Test-Path "$buildOutput\PluginConfiguration.xml") {
    Write-Host "  Copying PluginConfiguration.xml to plugin root..." -ForegroundColor Gray
    Copy-Item "$buildOutput\PluginConfiguration.xml" -Destination $packageDir
}
if (Test-Path "$buildOutput\PluginConfiguration.json") {
    Write-Host "  Copying PluginConfiguration.json to plugin root..." -ForegroundColor Gray
    Copy-Item "$buildOutput\PluginConfiguration.json" -Destination $packageDir
}

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
Write-Host "Package: $pluginName (version $Version)" -ForegroundColor Cyan
Write-Host ""
Write-Host "Installation:" -ForegroundColor Yellow
Write-Host "1. Close Loupedeck Software" -ForegroundColor White
Write-Host "2. Double-click on $pluginName" -ForegroundColor White
Write-Host "   OR drag & drop it into Loupedeck Software" -ForegroundColor White
Write-Host "3. Restart Loupedeck Software" -ForegroundColor White
Write-Host ""
Write-Host "Verify the update took effect:" -ForegroundColor Yellow
Write-Host "  Open %LocalAppData%\Loupedeck\Logs\ShellyPlugin_Debug.log" -ForegroundColor White
Write-Host "  The first lines must read: Plugin build: version $Version.0" -ForegroundColor White
Write-Host "  A different version means Loupedeck kept the old plugin." -ForegroundColor White
Write-Host ""
