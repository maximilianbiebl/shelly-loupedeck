# Loupedeck SDK Pfad finden

## Automatische Suche

Führe dies in PowerShell aus, um die PluginApi.dll zu finden:

```powershell
# Suche nach PluginApi.dll
Get-ChildItem -Path "C:\Program Files" -Filter "PluginApi.dll" -Recurse -ErrorAction SilentlyContinue | Select-Object FullName
Get-ChildItem -Path "C:\Program Files (x86)" -Filter "PluginApi.dll" -Recurse -ErrorAction SilentlyContinue | Select-Object FullName
```

## Häufige Pfade

Die PluginApi.dll befindet sich normalerweise hier:

**Loupedeck Version 6.0+:**
- `C:\Program Files\Loupedeck\Loupedeck2\PluginApi.dll`
- `C:\Program Files (x86)\Loupedeck\Loupedeck2\PluginApi.dll`

**Ältere Versionen:**
- `C:\Program Files\Loupedeck\PluginApi.dll`
- `C:\Program Files (x86)\Loupedeck\PluginApi.dll`

## Manuelle Anpassung

Falls deine Installation woanders liegt, kannst du den Pfad in der `.csproj` anpassen:

1. Öffne `ShellyLoupedeckPlugin.csproj` in einem Texteditor

2. Ändere die Zeile 12-14:
   ```xml
   <LoupedeckInstallFolder>DEIN_PFAD_HIER\</LoupedeckInstallFolder>
   ```

   Zum Beispiel:
   ```xml
   <LoupedeckInstallFolder>D:\Programme\Loupedeck\Loupedeck2\</LoupedeckInstallFolder>
   ```

## Als Umgebungsvariable

Du kannst auch eine Umgebungsvariable setzen:

```powershell
[System.Environment]::SetEnvironmentVariable("LoupedeckInstallFolder", "C:\Dein\Pfad\Loupedeck2\", "User")
```

Dann startest du deine IDE/PowerShell neu.
