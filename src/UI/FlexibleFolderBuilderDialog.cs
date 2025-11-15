using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ShellyLoupedeckPlugin.Models;

namespace ShellyLoupedeckPlugin.UI
{
    public class FlexibleFolderBuilderDialog : Form
    {
        private ShellyLoupedeckPlugin _plugin;
        private TextBox _infoTextBox;
        private Button _createExampleButton;
        private Button _closeButton;

        public FlexibleFolderBuilderDialog(ShellyLoupedeckPlugin plugin)
        {
            _plugin = plugin;
            InitializeUI();
        }

        private void InitializeUI()
        {
            Text = "Flexible Folder Builder (Beta)";
            Size = new Size(600, 400);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var infoLabel = new Label
            {
                Text = "Flexible Folder System - Quick Start",
                Location = new Point(20, 20),
                Size = new Size(560, 30),
                Font = new Font("Arial", 14, FontStyle.Bold)
            };
            Controls.Add(infoLabel);

            _infoTextBox = new TextBox
            {
                Location = new Point(20, 60),
                Size = new Size(560, 240),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Text = @"Das Flexible Folder System ermöglicht dir:

✓ Beliebig viele Ebenen/Menüs erstellen
✓ Jede Ebene mit eigenen Buttons bestücken
✓ Gerätenamen werden IMMER angezeigt
✓ Navigation-Buttons für Sub-Ebenen
✓ Direkt ausführbare Aktionen (Toggle, etc.)

AKTUELLER STATUS:
- FlexibleFolder1 wurde erstellt und ist bereit
- Modelle und Backend-Logik sind implementiert
- UI zum Bearbeiten folgt im nächsten Update

WIE DU ES NUTZT:
1. Klicke auf 'Create Example' um einen Testordner zu erstellen
2. Füge 'Flexible Folder 1' zu deinem Loupedeck hinzu
3. Der Beispielordner zeigt Navigations-Buttons (lila) und Device-Toggles (grün/grau)
4. Gerätenamen werden korrekt angezeigt!

NÄCHSTE SCHRITTE:
- Vollständiges Edit-UI wird im nächsten Update hinzugefügt
- Dann kannst du Ordner komplett selbst konfigurieren
- Support für alle Action-Typen (Brightness, Color, etc.)"
            };
            Controls.Add(_infoTextBox);

            _createExampleButton = new Button
            {
                Text = "Create Example Folder",
                Location = new Point(20, 320),
                Size = new Size(200, 35),
                Font = new Font("Arial", 10, FontStyle.Bold)
            };
            _createExampleButton.Click += CreateExampleButton_Click;
            Controls.Add(_createExampleButton);

            _closeButton = new Button
            {
                Text = "Close",
                Location = new Point(480, 320),
                Size = new Size(100, 35)
            };
            _closeButton.Click += (s, e) => Close();
            Controls.Add(_closeButton);
        }

        private void CreateExampleButton_Click(object sender, EventArgs e)
        {
            // Create a sample flexible folder with navigation
            var folder = new FlexibleFolderConfiguration
            {
                Name = "Example Flexible Folder"
            };

            // Main level - add some devices and a submenu
            folder.RootLevel.Name = "Main Menu";

            // Add first 3 devices as toggles
            var devices = _plugin.Devices.Take(3).ToList();
            foreach (var device in devices)
            {
                folder.RootLevel.Buttons.Add(new FlexibleButton(
                    device.Name, // Label will show device name
                    "DeviceToggle",
                    deviceId: device.Id
                ));
            }

            // Create a submenu
            var subLevel = new FlexibleFolderLevel
            {
                Name = "More Devices"
            };

            // Add more devices to submenu
            var moreDevices = _plugin.Devices.Skip(3).Take(5).ToList();
            foreach (var device in moreDevices)
            {
                subLevel.Buttons.Add(new FlexibleButton(
                    device.Name,
                    "DeviceToggle",
                    deviceId: device.Id
                ));
            }

            // Add navigation button to main level
            folder.RootLevel.Buttons.Add(new FlexibleButton(
                "More Devices →",
                subLevel
            ));

            // Save the folder
            _plugin.AddFlexibleFolder(folder);

            MessageBox.Show(
                "Example folder created!\n\n" +
                "Add 'Flexible Folder 1' to your Loupedeck to see it.\n\n" +
                "Features:\n" +
                "- Device names are displayed\n" +
                "- Navigation button (purple) opens submenu\n" +
                "- Toggle buttons (green/gray) control devices",
                "Success",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );

            Close();
        }
    }
}
