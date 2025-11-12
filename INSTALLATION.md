# Installation und Setup Guide

## Schritt 1: Voraussetzungen installieren

### .NET 8.0 SDK installieren

1. Gehe zu https://dotnet.microsoft.com/download/dotnet/8.0
2. Lade den ".NET 8.0 SDK" für dein System herunter (Windows x64)
3. Installiere das SDK
4. Überprüfe die Installation:
   ```powershell
   dotnet --version
   # Sollte 8.0.x anzeigen
   ```

### Loupedeck Software installieren

1. Gehe zu https://loupedeck.com/downloads/
2. Lade die neueste Version (6.0+) herunter
3. Installiere die Software
4. Starte Loupedeck und schließe es wieder

### Loupedeck SDK Pfad prüfen (WICHTIG!)

Das Plugin benötigt die **PluginApi.dll** aus der Loupedeck Installation.

**Schnelltest - Prüfe ob die Datei existiert:**
```powershell
# In PowerShell ausführen:
Test-Path "C:\Program Files\Loupedeck\Loupedeck2\PluginApi.dll"
# oder
Test-Path "C:\Program Files (x86)\Loupedeck\Loupedeck2\PluginApi.dll"
```

Sollte `True` zurückgeben. Falls `False`, siehe **SDK_PATH.md** für Hilfe.

## Schritt 2: Shelly Cloud API Key erhalten

### In der Shelly App (Smartphone)

1. Öffne die **Shelly Cloud App**
2. Gehe zu **Einstellungen** (unten rechts)
3. Tippe auf **Benutzereinstellungen**
4. Scrolle nach unten zu **Authorization Cloud Key**
5. **Notiere dir**:
   - Den **Authorization Key** (lange Zeichenkette)
   - Die **Server URL** (z.B. `https://shelly-28-eu.shelly.cloud`)

**Wichtig**: Der Auth Key ändert sich, wenn du dein Passwort änderst!

## Schritt 3: Plugin kompilieren

### Mit Visual Studio

1. Öffne `ShellyLoupedeckPlugin.sln` in Visual Studio 2022
2. Wähle **Release** und **x64** als Build-Konfiguration
3. Klicke auf **Build** → **Build Solution**
4. Die kompilierten Dateien findest du in:
   ```
   bin\Release\net8.0-windows\
   ```

### Mit der Kommandozeile

```powershell
# Im Projekt-Verzeichnis
cd shelly-loupedeck

# Release Build erstellen
dotnet build -c Release

# Output ist in:
# bin\Release\net8.0-windows\
```

## Schritt 4: Plugin installieren

### Windows Installation

1. **Loupedeck Software schließen** (wichtig!)

2. Öffne den Plugin-Ordner:
   ```powershell
   # Im Explorer öffnen:
   explorer %LocalAppData%\Loupedeck\Plugins
   ```

3. Erstelle einen neuen Ordner:
   ```
   %LocalAppData%\Loupedeck\Plugins\ShellyLoupedeckPlugin\
   ```

4. Kopiere alle Dateien aus `bin\Release\net8.0-windows\` in den neuen Ordner

5. Die Struktur sollte so aussehen:
   ```
   %LocalAppData%\Loupedeck\Plugins\ShellyLoupedeckPlugin\
   ├── ShellyLoupedeckPlugin.dll
   ├── ShellyLoupedeckPlugin.pdb
   ├── Newtonsoft.Json.dll
   ├── LoupedeckPackage.yaml
   └── metadata\
       ├── Icon16x16.png
       └── Icon256x256.png
   ```

## Schritt 5: Plugin konfigurieren

### Methode 1: Manuell über Datei (empfohlen)

1. Öffne den Plugin-Daten-Ordner:
   ```powershell
   explorer %LocalAppData%\Loupedeck\PluginData
   ```

2. Erstelle einen Ordner `ShellyLoupedeckPlugin` (falls nicht vorhanden)

3. Erstelle eine Datei `settings.json` mit folgendem Inhalt:
   ```json
   {
     "ServerUrl": "https://shelly-28-eu.shelly.cloud",
     "AuthKey": "DEIN_AUTH_KEY_HIER",
     "Groups": []
   }
   ```

4. Ersetze:
   - `https://shelly-28-eu.shelly.cloud` mit deiner Server URL
   - `DEIN_AUTH_KEY_HIER` mit deinem Authorization Key

### Methode 2: Über Loupedeck Software

1. Starte Loupedeck Software
2. Gehe zu **Einstellungen**
3. Suche nach **Shelly Cloud Control** Plugin
4. Trage die Werte ein:
   - Server URL
   - Auth Key

## Schritt 6: Plugin verwenden

1. **Starte die Loupedeck Software**

2. Das Plugin sollte automatisch geladen werden und deine Geräte abrufen

3. **Füge Actions hinzu**:
   - Öffne eine Seite/Page auf deinem Loupedeck
   - Klicke auf ein leeres Feld
   - Suche nach "Shelly"
   - Du solltest folgende Actions sehen:
     - Device Switch
     - RGBW Brightness
     - RGBW Color
     - Dimmer Brightness
     - Thermostat Temperature
     - Thermostat Boost
     - Device Overview

4. **Wähle deine Geräte**:
   - Bei jeder Action kannst du das Gerät auswählen
   - Geräte werden automatisch aus deinem Cloud-Account geladen

## Schritt 7: Gruppen erstellen (optional)

### Manuell in settings.json

```json
{
  "ServerUrl": "https://shelly-28-eu.shelly.cloud",
  "AuthKey": "DEIN_AUTH_KEY_HIER",
  "Groups": [
    {
      "id": "wohnzimmer-lichter",
      "name": "Wohnzimmer",
      "type": "RGBW",
      "deviceIds": [
        "abc123",
        "def456"
      ]
    },
    {
      "id": "alle-heizungen",
      "name": "Alle Heizungen",
      "type": "Thermostat",
      "deviceIds": [
        "thermo1",
        "thermo2",
        "thermo3"
      ]
    }
  ]
}
```

**Device-IDs finden**:
1. In der Shelly App
2. Gerät auswählen → **Einstellungen**
3. **Device Information** → **Device ID**

**Mögliche Device-Typen**:
- `Switch` - Normale Switches
- `RGBW` - RGBW Bulbs
- `Dimmer` - Dimmer
- `Thermostat` - Thermostate
- `ShellyPlus2PM` - Shelly Plus 2PM

## Troubleshooting

### Plugin wird nicht angezeigt

**Problem**: Plugin erscheint nicht in der Action-Liste

**Lösungen**:
1. Stelle sicher, dass Loupedeck Software vollständig geschlossen war beim Kopieren
2. Prüfe, ob alle DLL-Dateien korrekt kopiert wurden
3. Schaue in die Logs:
   ```powershell
   explorer %LocalAppData%\Loupedeck\Logs
   ```
4. Öffne die neueste Log-Datei und suche nach "Shelly" oder Fehlermeldungen

### Keine Geräte werden geladen

**Problem**: Actions sind da, aber keine Geräte erscheinen

**Lösungen**:
1. Prüfe deine `settings.json`:
   - Ist die Server URL korrekt?
   - Ist der Auth Key korrekt eingegeben?
   - Keine Leerzeichen am Anfang/Ende?

2. Teste die API manuell:
   ```powershell
   # In PowerShell:
   $url = "https://shelly-28-eu.shelly.cloud/device/list?auth_key=DEIN_KEY"
   Invoke-RestMethod -Uri $url
   ```

3. Prüfe die Loupedeck Logs auf API-Fehler

### Fehler beim Kompilieren

**Problem**: `PluginApi.dll` nicht gefunden oder "Das Paket Loupedeck.Plugin.SDK wurde nicht gefunden"

**Lösung**:
1. Prüfe, ob die PluginApi.dll existiert:
   ```powershell
   Test-Path "C:\Program Files\Loupedeck\Loupedeck2\PluginApi.dll"
   ```

2. Falls False, suche nach der DLL:
   ```powershell
   Get-ChildItem -Path "C:\Program Files" -Filter "PluginApi.dll" -Recurse -ErrorAction SilentlyContinue
   ```

3. Passe den Pfad in `ShellyLoupedeckPlugin.csproj` an:
   - Öffne die Datei in einem Texteditor
   - Ändere Zeile 12-14 mit dem korrekten Pfad
   - Siehe **SDK_PATH.md** für Details

4. Baue erneut:
   ```powershell
   dotnet restore
   dotnet build -c Release
   ```

**Problem**: .NET 8.0 nicht gefunden

**Lösung**:
- Installiere .NET 8.0 SDK (siehe Schritt 1)
- Öffne eine neue PowerShell/CMD (damit PATH aktualisiert wird)

### Geräte reagieren nicht

**Problem**: Actions erscheinen, aber Geräte reagieren nicht auf Befehle

**Lösungen**:
1. Prüfe, ob Geräte in der Shelly App online sind
2. API Rate Limit beachten (1 Request/Sekunde)
3. Warte 30 Sekunden - Plugin aktualisiert Status automatisch

### Settings werden nicht gespeichert

**Problem**: Änderungen in settings.json werden nicht übernommen

**Lösungen**:
1. Loupedeck Software komplett beenden
2. `settings.json` bearbeiten
3. Loupedeck Software neu starten

## Erweiterte Konfiguration

### Debug-Modus aktivieren

Für Entwickler: Kompiliere im Debug-Modus:

```powershell
dotnet build -c Debug
```

Dann kannst du mit Visual Studio debuggen:
1. Öffne das Projekt in VS 2022
2. Setze Breakpoints
3. Drücke F5 (Debug starten)
4. Loupedeck Software startet automatisch

### Custom Icons verwenden

Ersetze die Dateien in `metadata/`:
- `Icon16x16.png` - Wird in der Plugin-Liste angezeigt
- `Icon256x256.png` - Wird in Details angezeigt

Anforderungen:
- PNG Format
- Transparenter Hintergrund
- Exakte Größen (16x16 und 256x256)

### Polling-Intervall ändern

In `ShellyLoupedeckPlugin.cs` die Zeile ändern:

```csharp
// Von 30 Sekunden (30000 ms)
TimeSpan.FromSeconds(30)

// Zu z.B. 10 Sekunden
TimeSpan.FromSeconds(10)
```

**Achtung**: Kürzere Intervalle bedeuten mehr API-Anfragen!

## Weitere Hilfe

- **GitHub Issues**: https://github.com/maximilianbiebl/shelly-loupedeck/issues
- **Loupedeck Forum**: https://support.loupedeck.com/
- **Shelly Support**: https://support.shelly.cloud/

## Nächste Schritte

Nach erfolgreicher Installation:

1. ✅ Erstelle deine erste Page mit Shelly-Actions
2. ✅ Gruppiere zusammengehörige Geräte
3. ✅ Teste verschiedene Action-Typen (Switch, Brightness, Color)
4. ✅ Nutze den Device Overview für schnellen Zugriff
5. ✅ Optimiere dein Layout nach deinen Bedürfnissen

Viel Spaß mit deinem Shelly Loupedeck Plugin! 🎉
