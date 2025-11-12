# Shelly Loupedeck Plugin

Ein Loupedeck Plugin zur Steuerung von Shelly Smart Home Geräten über die Shelly Cloud API.

## Features

### Unterstützte Geräte
- **RGBW Bulbs**: Helligkeit und Farbe steuern
- **Dimmer**: Helligkeit über Drehrad einstellen
- **Thermostate**: Temperatur einstellen und Boost-Modus aktivieren
- **Switches**: Ein/Aus-Schalter für normale Shellys
- **Shelly Plus 2PM**: Spezielle Steuerung für Plus 2PM Geräte

### Funktionen
- 🎛️ **Drehrad-Steuerung** für Helligkeit und Temperatur
- 🎨 **Farbsteuerung** für RGBW Bulbs mit vordefinierten Farben
- 📱 **Touch-Buttons** zum Ein-/Ausschalten
- 🔥 **Boost-Modus** für Thermostate (30/60/120 Minuten)
- 👥 **Gruppen-Verwaltung** für gleichzeitige Steuerung mehrerer Geräte
- 🔄 **Automatische Geräte-Erkennung** aus deinem Shelly Cloud Account
- 📊 **Device Overview** - Task-Switcher-ähnliche Übersicht aller Geräte

## Installation

### ⚡ Schnellinstallation (Empfohlen)

**Für Nutzer - Fertig gepacktes Plugin:**

1. **Plugin bauen und packen:**
   ```powershell
   git clone https://github.com/maximilianbiebl/shelly-loupedeck.git
   cd shelly-loupedeck
   .\package.ps1
   ```

2. **Plugin installieren:**
   - Doppelklick auf `ShellyCloudControl.lplug4`
   - ODER: Drag & Drop in Loupedeck Software
   - ODER: Loupedeck → Einstellungen → Plugins → Install Plugin from File

3. **Fertig!** Das Plugin ist installiert.

Siehe **[PACKAGE_INSTALL.md](PACKAGE_INSTALL.md)** für Details.

---

### 🔧 Entwickler-Installation

**Für Entwickler - Manuell aus Source:**

#### Voraussetzungen
- Loupedeck Software (Version 6.0 oder höher)
- .NET Framework 4.7.2 SDK
- Visual Studio 2022 oder JetBrains Rider
- Shelly Cloud Account mit API Key

#### Kompilieren

```powershell
# Repository klonen
git clone https://github.com/maximilianbiebl/shelly-loupedeck.git
cd shelly-loupedeck

# Projekt bauen
dotnet build -c Release
```

#### Manuelle Installation

1. Loupedeck Software schließen
2. Plugin-Dateien kopieren nach:
   ```
   %LocalAppData%\Loupedeck\Plugins\ShellyCloudControl\
   ```
3. Loupedeck Software starten

Siehe **[INSTALLATION.md](INSTALLATION.md)** für Details.

---

### 📋 Shelly Cloud API Key erhalten

1. Öffne die Shelly Cloud App auf deinem Smartphone
2. Gehe zu **Einstellungen** → **Benutzereinstellungen**
3. Finde den **Authorization Cloud Key**
4. Notiere auch die **Server URL** (z.B. `https://shelly-28-eu.shelly.cloud`)

## Konfiguration

### API Zugangsdaten konfigurieren

Da das Plugin aktuell keine grafische Settings-UI hat, musst du die Zugangsdaten manuell in der Plugin-Config eintragen:

1. Öffne die Loupedeck Plugin-Einstellungen
2. Suche nach dem Shelly Plugin
3. Trage ein:
   - **Server URL**: Deine Shelly Cloud Server URL (z.B. `https://shelly-28-eu.shelly.cloud`)
   - **Auth Key**: Dein Authorization Cloud Key

Alternativ kannst du die Datei direkt bearbeiten:
- Windows: `%LocalAppData%\Loupedeck\PluginData\ShellyCloudControl\settings.json`

```json
{
  "ServerUrl": "https://shelly-28-eu.shelly.cloud",
  "AuthKey": "DEIN_AUTH_KEY_HIER"
}
```

### Gruppen erstellen

Gruppen werden im Plugin automatisch gespeichert. Du kannst Gruppen manuell in der `settings.json` erstellen:

```json
{
  "ServerUrl": "https://shelly-28-eu.shelly.cloud",
  "AuthKey": "DEIN_AUTH_KEY_HIER",
  "Groups": [
    {
      "id": "gruppe-1",
      "name": "Wohnzimmer Lichter",
      "type": "RGBW",
      "deviceIds": ["device-id-1", "device-id-2"]
    }
  ]
}
```

## Verwendung

### Verfügbare Actions

#### Device Switch
- **Beschreibung**: Ein/Aus-Schalter für Geräte
- **Unterstützt**: Switches, RGBW, Shelly Plus 2PM
- **Verwendung**: Drücken zum Umschalten
- **Visuell**: Grün = An, Schwarz = Aus

#### RGBW Brightness
- **Beschreibung**: Helligkeit von RGBW Bulbs einstellen
- **Control**: Drehrad
- **Bereich**: 0-100%
- **Schrittweite**: 5% pro Drehschritt

#### RGBW Color
- **Beschreibung**: Farbe von RGBW Bulbs einstellen
- **Verfügbare Farben**:
  - Rot, Grün, Blau
  - Gelb, Cyan, Magenta
  - Weiß, Warm White, Cool White

#### Dimmer Brightness
- **Beschreibung**: Helligkeit von Dimmern einstellen
- **Control**: Drehrad
- **Bereich**: 0-100%

#### Thermostat Temperature
- **Beschreibung**: Zieltemperatur einstellen
- **Control**: Drehrad
- **Bereich**: 5-30°C
- **Schrittweite**: 0.5°C pro Drehschritt

#### Thermostat Boost
- **Beschreibung**: Boost-Modus aktivieren
- **Optionen**: 30, 60, 120 Minuten
- **Visuell**: Orange Button

#### Device Overview
- **Beschreibung**: Übersicht aller Geräte
- **Kategorien**:
  - All Devices
  - All Switches
  - All Lights
  - All Thermostats
  - All Groups

## Entwicklung

### Projekt-Struktur

```
shelly-loupedeck/
├── src/
│   ├── Api/
│   │   └── ShellyApiClient.cs          # Shelly Cloud API Client
│   ├── Models/
│   │   ├── ShellyDevice.cs             # Device Models
│   │   └── DeviceGroup.cs              # Group Models
│   ├── Actions/
│   │   ├── DeviceSwitchAction.cs       # On/Off Switch
│   │   ├── RGBWBrightnessAdjustment.cs # RGBW Helligkeit
│   │   ├── RGBWColorAdjustment.cs      # RGBW Farbe
│   │   ├── DimmerAdjustment.cs         # Dimmer Helligkeit
│   │   ├── ThermostatAdjustment.cs     # Thermostat Temperatur
│   │   ├── ThermostatBoostAction.cs    # Thermostat Boost
│   │   └── DeviceOverviewCommand.cs    # Device Overview
│   ├── Commands/
│   │   └── SettingsCommand.cs          # Settings Command
│   └── ShellyLoupedeckPlugin.cs        # Main Plugin Class
├── metadata/
│   ├── Icon16x16.png
│   └── Icon256x256.png
├── LoupedeckPackage.yaml               # Plugin Manifest
├── ShellyLoupedeckPlugin.csproj        # C# Project
└── README.md
```

### API Rate Limiting

Die Shelly Cloud API hat ein Rate Limit von **1 Request pro Sekunde**. Das Plugin implementiert automatisches Rate Limiting.

### Debugging

1. Öffne das Projekt in Visual Studio 2022
2. Setze Breakpoints in deinen Actions
3. Starte Debug-Session
4. Loupedeck Software sollte das Plugin automatisch laden

### Logging

Debug-Ausgaben werden in die Visual Studio Output-Console geschrieben:

```csharp
System.Diagnostics.Debug.WriteLine("Debug message");
```

## Bekannte Einschränkungen

- Keine grafische UI für Settings (manuelle Konfiguration erforderlich)
- Gruppen können aktuell nur manuell in der Config erstellt werden
- Device-Status wird alle 30 Sekunden aktualisiert
- API Rate Limit: 1 Request/Sekunde

## Zukünftige Features

- [ ] Grafische Settings-UI
- [ ] Drag & Drop Gruppen-Erstellung
- [ ] Szenen-Unterstützung
- [ ] Custom Icon Upload
- [ ] Erweiterte Zeitsteuerung
- [ ] Webhook-Support für Echtzeit-Updates

## Troubleshooting

### Plugin wird nicht geladen
- Stelle sicher, dass .NET Framework 4.7.2 installiert ist
- Prüfe die Loupedeck Software Version (min. 6.0)
- Schaue in die Loupedeck Logs: `%LocalAppData%\Loupedeck\Logs\`

### Geräte werden nicht angezeigt
- Prüfe deinen Auth Key
- Stelle sicher, dass die Server URL korrekt ist
- Prüfe deine Internetverbindung
- Schaue in Debug-Ausgabe nach Fehlermeldungen

### Befehle funktionieren nicht
- API Rate Limit könnte überschritten sein (warte 1 Sekunde)
- Gerät könnte offline sein
- Prüfe Device Status in Shelly App

## Support

Bei Fragen oder Problemen:
- GitHub Issues: https://github.com/maximilianbiebl/shelly-loupedeck/issues

## Lizenz

MIT License - siehe LICENSE Datei

## Credits

Entwickelt für Loupedeck Live mit der Loupedeck Plugin SDK v6.
Verwendet die Shelly Cloud API v2.0.
