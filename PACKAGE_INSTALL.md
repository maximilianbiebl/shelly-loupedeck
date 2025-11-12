# Plugin Installation - .lplug4 Package

## Schnellinstallation

### Schritt 1: Plugin Package erstellen

```powershell
cd C:\Users\Maximilian\shelly-loupedeck

# Plugin bauen und als .lplug4 packen
.\package.ps1
```

Das erstellt eine Datei: `ShellyCloudControl.lplug4`

### Schritt 2: Plugin in Loupedeck installieren

**Option A: Doppelklick**
1. Schließe Loupedeck Software
2. Doppelklicke auf `ShellyCloudControl.lplug4`
3. Loupedeck Software startet automatisch und installiert das Plugin

**Option B: Drag & Drop**
1. Öffne Loupedeck Software
2. Ziehe `ShellyCloudControl.lplug4` in das Loupedeck Fenster
3. Plugin wird automatisch installiert

**Option C: Manuell über Menü**
1. Öffne Loupedeck Software
2. Gehe zu **Einstellungen** → **Plugins**
3. Klicke auf **Install Plugin from File**
4. Wähle `ShellyCloudControl.lplug4` aus

### Schritt 3: Plugin konfigurieren

Nach der Installation:

1. Erstelle die Konfigurationsdatei:
   ```powershell
   # Erstelle Ordner
   New-Item -ItemType Directory -Path "$env:LOCALAPPDATA\Loupedeck\PluginData\ShellyCloudControl" -Force

   # Erstelle settings.json
   @"
   {
     "ServerUrl": "https://shelly-28-eu.shelly.cloud",
     "AuthKey": "DEIN_AUTH_KEY_HIER",
     "Groups": []
   }
   "@ | Out-File -FilePath "$env:LOCALAPPDATA\Loupedeck\PluginData\ShellyCloudControl\settings.json" -Encoding UTF8
   ```

2. Öffne die Datei und trage deinen Auth Key ein:
   ```powershell
   notepad "$env:LOCALAPPDATA\Loupedeck\PluginData\ShellyCloudControl\settings.json"
   ```

3. Starte Loupedeck Software neu

### Schritt 4: Plugin verwenden

1. Öffne eine Seite auf deinem Loupedeck
2. Klicke auf ein leeres Feld
3. Suche nach "Shelly"
4. Wähle eine Action aus:
   - **Device Switch** - Ein/Aus Schalter
   - **RGBW Brightness** - Helligkeit über Drehrad
   - **RGBW Color** - Farbauswahl
   - **Dimmer Brightness** - Dimmer Helligkeit
   - **Thermostat Temperature** - Temperatur einstellen
   - **Thermostat Boost** - Boost Modus
   - **Device Overview** - Geräte-Übersicht

## Troubleshooting

### Plugin wird nicht angezeigt

1. **Prüfe die Logs**:
   ```powershell
   explorer "$env:LOCALAPPDATA\Loupedeck\Logs"
   ```
   Öffne die neueste Log-Datei und suche nach "Shelly" oder Fehlermeldungen

2. **Prüfe ob Plugin installiert ist**:
   ```powershell
   Test-Path "$env:LOCALAPPDATA\Loupedeck\Plugins\ShellyCloudControl"
   ```
   Sollte `True` zurückgeben

3. **Manuelle Installation**:
   Falls die automatische Installation nicht funktioniert:
   ```powershell
   # .lplug4 ist eigentlich ein ZIP - entpacke es:
   Expand-Archive -Path "ShellyCloudControl.lplug4" -DestinationPath "$env:LOCALAPPDATA\Loupedeck\Plugins\ShellyCloudControl" -Force
   ```

### Keine Geräte werden angezeigt

1. Prüfe `settings.json`:
   ```powershell
   notepad "$env:LOCALAPPDATA\Loupedeck\PluginData\ShellyCloudControl\settings.json"
   ```

2. Teste API-Verbindung:
   ```powershell
   $authKey = "DEIN_AUTH_KEY"
   $url = "https://shelly-28-eu.shelly.cloud/device/list?auth_key=$authKey"
   Invoke-RestMethod -Uri $url
   ```

### Plugin neu bauen

Nach Code-Änderungen:

```powershell
# Loupedeck schließen
Get-Process "Loupedeck" | Stop-Process -Force

# Plugin neu bauen und packen
.\package.ps1 -Clean

# Plugin manuell installieren
Expand-Archive -Path "ShellyCloudControl.lplug4" -DestinationPath "$env:LOCALAPPDATA\Loupedeck\Plugins\ShellyCloudControl" -Force

# Loupedeck starten
Start-Process "C:\Program Files\Loupedeck\Loupedeck.exe"
# oder
Start-Process "C:\Program Files (x86)\Loupedeck\Loupedeck.exe"
```

## Was ist eine .lplug4 Datei?

Eine `.lplug4` Datei ist ein Loupedeck Plugin Package. Es ist eigentlich ein ZIP-Archiv mit folgender Struktur:

```
ShellyCloudControl.lplug4
├── LoupedeckPackage.yaml      # Plugin Manifest
├── metadata/
│   ├── Icon16x16.png          # Kleines Icon
│   └── Icon256x256.png        # Großes Icon
└── win/                       # Windows Binaries
    ├── ShellyLoupedeckPlugin.dll
    ├── ShellyLoupedeckPlugin.pdb
    └── Newtonsoft.Json.dll
```

## Shelly Cloud API Key erhalten

1. Öffne die **Shelly Cloud App** auf deinem Smartphone
2. Gehe zu **Einstellungen** → **Benutzereinstellungen**
3. Scrolle zu **Authorization Cloud Key**
4. Kopiere den Key
5. Notiere auch die **Server URL** (z.B. `https://shelly-28-eu.shelly.cloud`)

## Device IDs für Gruppen finden

In der Shelly App:
1. Wähle ein Gerät aus
2. Gehe zu **Einstellungen**
3. **Device Information** → **Device ID**
4. Kopiere die ID

Dann in `settings.json`:
```json
{
  "ServerUrl": "https://shelly-28-eu.shelly.cloud",
  "AuthKey": "dein_key",
  "Groups": [
    {
      "id": "wohnzimmer-lichter",
      "name": "Wohnzimmer",
      "type": "RGBW",
      "deviceIds": ["device-id-1", "device-id-2"]
    }
  ]
}
```

## Verfügbare Geräte-Typen für Gruppen

- `Switch` - Normale Schalter
- `RGBW` - RGBW Bulbs
- `Dimmer` - Dimmer
- `Thermostat` - Thermostate
- `ShellyPlus2PM` - Shelly Plus 2PM

## Nächste Schritte

Nach erfolgreicher Installation:

1. ✅ Konfiguriere deine API Keys
2. ✅ Füge deine ersten Actions hinzu
3. ✅ Erstelle Gruppen für mehrere Geräte
4. ✅ Passe dein Loupedeck Layout an
5. ✅ Genieße die Smart Home Steuerung!

Bei Problemen: https://github.com/maximilianbiebl/shelly-loupedeck/issues
