# Folder Builder Command Fix

## Problem

FolderBuilderCommand war nicht in Loupedeck sichtbar, obwohl es korrekt implementiert war.

## Root Cause (Hauptursache)

**KRITISCHER FEHLER**: Das `build.ps1` Script verwendete den falschen Build-Output-Pfad!

- **.csproj** erstellt Dateien in: `bin\Release\net472\`
- **build.ps1** suchte aber in: `bin\Release\net8.0-windows\`

**Resultat**: Wenn du `.\build.ps1 -Install` verwendet hast, wurde entweder:
1. Nichts installiert (Verzeichnis existiert nicht)
2. Alte/veraltete DLL-Dateien wurden kopiert

Die aktualisierte DLL mit FolderBuilderCommand wurde **nie** in Loupedeck installiert!

## Fixes Applied (Durchgeführte Korrekturen)

### 1. Build Script Path Korrektur (KRITISCH!)
- ✅ `build.ps1`: Korrigiert zu `bin\$config\net472`
- ✅ `INSTALLATION.md`: Dokumentation aktualisiert mit korrekten Pfaden

### 2. FolderBuilderCommand Optimierungen
- ✅ Verwendet jetzt `base()` Constructor mit Parametern (wie GroupManagementCommand)
- ✅ GroupName geändert zu "Folders" (statt "Configuration")
- ✅ Namespace: `ShellyLoupedeckPlugin.Commands`
- ✅ Location: `src/Commands/FolderBuilderCommand.cs`

### 3. Zusätzliche Diagnostic Tools
- ✅ **TestCommand**: Minimales Test-Command mit auffälligen Logs
- ✅ **FolderConfigCommand**: Vereinfachte Alternative zu FolderBuilderCommand

## Installation (WICHTIG!)

### Option 1: Mit build.ps1 (EMPFOHLEN)

```powershell
# 1. Loupedeck komplett schließen
Get-Process "Loupedeck*" | Stop-Process -Force

# 2. Neuester Code holen
git pull origin claude/shelly-device-refresh-fix-01XirYCxwZcquhG1U4nxxTbA

# 3. Clean Build durchführen
.\build.ps1 -Clean -Install

# 4. Loupedeck starten
Start-Process "$env:LOCALAPPDATA\Loupedeck\Loupedeck.exe"
```

### Option 2: Manuell

```powershell
# 1. Loupedeck schließen
Get-Process "Loupedeck*" | Stop-Process -Force

# 2. Code holen
git pull origin claude/shelly-device-refresh-fix-01XirYCxwZcquhG1U4nxxTbA

# 3. Build
dotnet clean
dotnet build -c Release

# 4. Plugin-Ordner leeren
Remove-Item "$env:LOCALAPPDATA\Loupedeck\Plugins\ShellyLoupedeckPlugin\*" -Recurse -Force

# 5. Neue Dateien kopieren
Copy-Item -Path "bin\Release\net472\*" -Destination "$env:LOCALAPPDATA\Loupedeck\Plugins\ShellyLoupedeckPlugin\" -Recurse -Force

# 6. Loupedeck starten
Start-Process "$env:LOCALAPPDATA\Loupedeck\Loupedeck.exe"
```

## Was sollte jetzt funktionieren

Nach der korrekten Installation solltest du folgende Commands in Loupedeck sehen:

### Im "Folders" Gruppe:
1. **Folder Builder** - Hauptinterface zum Erstellen/Bearbeiten von Ordnern
2. **Folder Config** - Vereinfachte Alternative (zeigt nur Folder-Count)

### Im "Configuration" Gruppe:
3. **TEST COMMAND** - Diagnostics (roter Button mit "TEST" Text)

### Existing Commands:
4. **Manage Groups** - Bestehender Group Management Command
5. **Shelly Settings** - Bestehender Settings Command

## Verwendung: Folder Builder

Wenn FolderBuilderCommand jetzt erscheint:

1. **Command hinzufügen**:
   - Öffne Loupedeck
   - Gehe zu Actions/Commands
   - Suche "Folder Builder" in der "Folders" Gruppe
   - Ziehe es auf ein Feld

2. **Neuen Folder erstellen**:
   - Klicke auf "Create New Folder"
   - Ein Dialog erscheint mit Bestätigung
   - Der neue Folder wird erstellt

3. **Folder bearbeiten**:
   - Klicke auf "Edit: [Folder Name]"
   - Optionen:
     - ➕ Add Button - Fügt Button hinzu
     - ➖ Remove Last Button - Entfernt letzten Button
     - 🗑 Delete Folder - Löscht Folder
     - ⬅ Back - Zurück zum Hauptmenü

4. **Button-Typen hinzufügen**:
   - **Any Plugin Action** - JEDE Action aus dem Plugin
   - **Device Toggle** - Device Ein/Aus
   - **Group Color** - Gruppen-Farbe setzen
   - **Group Brightness** - Gruppen-Helligkeit
   - **Group Toggle** - Alle Geräte in Gruppe

5. **Custom Folder platzieren**:
   - Nachdem Folder konfiguriert wurde
   - Gehe zu Actions → Folders
   - Wähle "Custom Folder 1" bis "Custom Folder 10"
   - CustomFolder1 zeigt den ersten konfigurierten Folder, etc.

## Debugging

Falls FolderBuilderCommand IMMER NOCH nicht erscheint:

### 1. Prüfe ob DLL korrekt installiert wurde

```powershell
# Prüfe ob die neueste DLL installiert ist
$pluginDll = "$env:LOCALAPPDATA\Loupedeck\Plugins\ShellyLoupedeckPlugin\ShellyLoupedeckPlugin.dll"
(Get-Item $pluginDll).LastWriteTime

# Sollte das heutige Datum/Zeit zeigen (nach dem Build)
```

### 2. Prüfe Loupedeck Logs

```powershell
# Öffne Log-Verzeichnis
explorer "$env:LOCALAPPDATA\Loupedeck\Logs"

# Suche nach:
# - "!!! FOLDER CONFIG COMMAND CONSTRUCTOR !!!"
# - "!!! TEST COMMAND CONSTRUCTOR !!!"
# - "=== FolderBuilderCommand constructor called ==="
```

### 3. Welche Commands erscheinen?

| Command | Erscheint? | Bedeutung |
|---------|-----------|-----------|
| TEST COMMAND | ✅ JA | Commands-Folder wird gescannt |
| Folder Config | ✅ JA | Einfache Commands funktionieren |
| Folder Builder | ❌ NEIN | Problem spezifisch mit FolderBuilderCommand |
| ALLE | ❌ NEIN | Commands-Folder wird NICHT gescannt → Build/Install Problem |

### 4. Letzte Diagnose

Wenn NICHTS erscheint:

```powershell
# 1. Prüfe ob PluginApi.dll Referenz korrekt ist
Test-Path "C:\Program Files\Loupedeck\Loupedeck2\PluginApi.dll"

# 2. Baue erneut mit verbose output
dotnet build -c Release -v detailed > build.log 2>&1

# 3. Suche nach Fehlern in build.log
Select-String -Path build.log -Pattern "error|warning.*Command"

# 4. Prüfe ob alle C# Dateien kompiliert wurden
Select-String -Path build.log -Pattern "FolderBuilderCommand|TestCommand|FolderConfigCommand"
```

## Zusammenfassung

**Die Hauptursache war**: Falscher Build-Output-Pfad im `build.ps1` Script.

**Die Lösung ist**:
1. Code pullen (korrekte build.ps1 Version)
2. Clean Build durchführen
3. Mit korrektem Pfad (`bin\Release\net472\`) installieren
4. Loupedeck neu starten

Nach diesen Schritten sollten **alle drei** neuen Commands erscheinen:
- ✅ Folder Builder
- ✅ Folder Config
- ✅ TEST COMMAND

Viel Erfolg! 🎉
