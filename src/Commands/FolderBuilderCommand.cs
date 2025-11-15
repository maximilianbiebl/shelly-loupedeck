using System;
using System.Collections.Generic;
using System.Linq;
using Loupedeck;
using ShellyLoupedeckPlugin.Models;

namespace ShellyLoupedeckPlugin.Commands
{
    public class FolderBuilderCommand : PluginDynamicCommand
    {
        private ShellyLoupedeckPlugin _plugin;
        private string _currentState = null; // Tracks current menu state
        private string _selectedFolderId = null; // Currently selected folder for editing

        public FolderBuilderCommand()
        {
            DisplayName = "Folder Builder";
            Description = "Create and customize control folders";
            GroupName = "Configuration";
        }

        protected override bool OnLoad()
        {
            _plugin = (ShellyLoupedeckPlugin)Plugin;
            _plugin.FoldersUpdated += OnFoldersUpdated;
            CreateParameters();
            return base.OnLoad();
        }

        protected override bool OnUnload()
        {
            _plugin.FoldersUpdated -= OnFoldersUpdated;
            return base.OnUnload();
        }

        private void OnFoldersUpdated(object sender, EventArgs e)
        {
            CreateParameters();
        }

        private void CreateParameters()
        {
            RemoveAllParameters();

            if (string.IsNullOrEmpty(_currentState))
            {
                // Main menu
                AddParameter("create_new", "Create New Folder", "Main Menu");

                // Show existing folders for editing
                for (int i = 0; i < _plugin.Folders.Count && i < 10; i++)
                {
                    var folder = _plugin.Folders[i];
                    AddParameter($"edit_{folder.Id}", $"Edit: {folder.Name} ({folder.Buttons.Count} buttons)", "Edit Folders");
                }
            }
            else if (_currentState.StartsWith("edit_"))
            {
                // Folder editor menu
                var folderId = _currentState.Substring(5);
                var folder = _plugin.Folders.FirstOrDefault(f => f.Id == folderId);

                if (folder != null)
                {
                    AddParameter("add_button", "➕ Add Button", "Actions");

                    if (folder.Buttons.Count > 0)
                        AddParameter("remove_last", "➖ Remove Last Button", "Actions");

                    AddParameter("delete_folder", "🗑 Delete Folder", "Actions");
                    AddParameter("back", "⬅ Back to Main Menu", "Navigation");
                }
            }
            else if (_currentState == "select_button_type")
            {
                // Button type selection
                AddParameter("type_plugin_action", "➕ Any Plugin Action", "Button Types");
                AddParameter("", "", "───────────");
                AddParameter("type_device_toggle", "Device Toggle", "Quick Actions");
                AddParameter("type_group_color", "Group Color", "Quick Actions");
                AddParameter("type_group_brightness", "Group Brightness", "Quick Actions");
                AddParameter("type_group_toggle", "Group Toggle", "Quick Actions");
                AddParameter("back", "⬅ Back", "Navigation");
            }
            else if (_currentState == "select_device")
            {
                // Device selection for toggle
                foreach (var device in _plugin.Devices)
                {
                    AddParameter($"device_{device.Id}", device.Name, "Devices");
                }
                AddParameter("back", "⬅ Back", "Navigation");
            }
            else if (_currentState == "select_group_color")
            {
                // Color selection
                var colorGroups = _plugin.Groups.Where(g => g.Purpose == GroupPurpose.Color);
                foreach (var group in colorGroups)
                {
                    AddParameter($"color_red_{group.Id}", $"{group.Name} - Red", "Colors");
                    AddParameter($"color_green_{group.Id}", $"{group.Name} - Green", "Colors");
                    AddParameter($"color_blue_{group.Id}", $"{group.Name} - Blue", "Colors");
                    AddParameter($"color_white_{group.Id}", $"{group.Name} - White", "Colors");
                    AddParameter($"color_yellow_{group.Id}", $"{group.Name} - Yellow", "Colors");
                    AddParameter($"color_cyan_{group.Id}", $"{group.Name} - Cyan", "Colors");
                    AddParameter($"color_magenta_{group.Id}", $"{group.Name} - Magenta", "Colors");
                }
                AddParameter("back", "⬅ Back", "Navigation");
            }
            else if (_currentState == "select_group_brightness")
            {
                // Brightness selection
                var dimmerGroups = _plugin.Groups.Where(g => g.Purpose == GroupPurpose.Color || g.Purpose == GroupPurpose.Brightness);
                foreach (var group in dimmerGroups)
                {
                    AddParameter($"bright_25_{group.Id}", $"{group.Name} - 25%", "Brightness");
                    AddParameter($"bright_50_{group.Id}", $"{group.Name} - 50%", "Brightness");
                    AddParameter($"bright_75_{group.Id}", $"{group.Name} - 75%", "Brightness");
                    AddParameter($"bright_100_{group.Id}", $"{group.Name} - 100%", "Brightness");
                }
                AddParameter("back", "⬅ Back", "Navigation");
            }
            else if (_currentState == "select_group_toggle")
            {
                // Group toggle selection
                foreach (var group in _plugin.Groups)
                {
                    AddParameter($"grouptoggle_{group.Id}", $"{group.Name} (All)", "Groups");
                }
                AddParameter("back", "⬅ Back", "Navigation");
            }
            else if (_currentState == "select_plugin_action")
            {
                // Show all available plugin actions
                // Device Actions
                foreach (var device in _plugin.Devices)
                {
                    AddParameter($"action_DeviceSwitchAction_{device.Id}", device.Name, "Device Switch");
                }

                // RGBW Actions
                var rgbwDevices = _plugin.Devices.Where(d => d.GetDeviceType() == ShellyDeviceType.RGBW);
                foreach (var device in rgbwDevices)
                {
                    AddParameter($"action_RGBWModeToggle_{device.Id}", $"{device.Name} - Mode Toggle", "RGBW");
                    AddParameter($"action_RGBWColorAdjustment_{device.Id}", $"{device.Name} - Color", "RGBW");
                }

                // Dimmer Actions
                var dimmerDevices = _plugin.Devices.Where(d => d.GetDeviceType() == ShellyDeviceType.Dimmer || d.GetDeviceType() == ShellyDeviceType.RGBW);
                foreach (var device in dimmerDevices)
                {
                    AddParameter($"action_DimmerAdjustment_{device.Id}", $"{device.Name} - Dimmer", "Dimmer");
                }

                // Thermostat Actions
                var thermostatDevices = _plugin.Devices.Where(d => d.GetDeviceType() == ShellyDeviceType.Thermostat);
                foreach (var device in thermostatDevices)
                {
                    AddParameter($"action_ThermostatAdjustment_{device.Id}", $"{device.Name} - Adjust", "Thermostat");
                    AddParameter($"action_ThermostatBoostAction_{device.Id}", $"{device.Name} - Boost", "Thermostat");
                }

                // Overview/Management
                AddParameter("action_DeviceOverviewCommand_", "Device Overview", "Management");
                AddParameter("action_GroupManagementCommand_", "Group Management", "Management");
                AddParameter("action_SettingsCommand_", "Settings", "Management");

                AddParameter("back", "⬅ Back", "Navigation");
            }

            ActionImageChanged();
        }

        protected override void RunCommand(string actionParameter)
        {
            if (string.IsNullOrEmpty(actionParameter))
                return;

            // Navigation
            if (actionParameter == "back")
            {
                if (_currentState == "select_button_type" ||
                    _currentState == "select_device" ||
                    _currentState.StartsWith("select_group"))
                {
                    // Back to folder editor
                    _currentState = $"edit_{_selectedFolderId}";
                }
                else
                {
                    // Back to main menu
                    _currentState = null;
                    _selectedFolderId = null;
                }
                CreateParameters();
                return;
            }

            // Main menu actions
            if (string.IsNullOrEmpty(_currentState))
            {
                if (actionParameter == "create_new")
                {
                    CreateNewFolder();
                }
                else if (actionParameter.StartsWith("edit_"))
                {
                    _selectedFolderId = actionParameter.Substring(5);
                    _currentState = $"edit_{_selectedFolderId}";
                    CreateParameters();
                }
                return;
            }

            // Folder editor actions
            if (_currentState.StartsWith("edit_"))
            {
                if (actionParameter == "add_button")
                {
                    _currentState = "select_button_type";
                    CreateParameters();
                }
                else if (actionParameter == "remove_last")
                {
                    RemoveLastButton();
                }
                else if (actionParameter == "delete_folder")
                {
                    DeleteFolder();
                }
                return;
            }

            // Button type selection
            if (_currentState == "select_button_type")
            {
                if (actionParameter == "type_device_toggle")
                {
                    _currentState = "select_device";
                    CreateParameters();
                }
                else if (actionParameter == "type_group_color")
                {
                    _currentState = "select_group_color";
                    CreateParameters();
                }
                else if (actionParameter == "type_group_brightness")
                {
                    _currentState = "select_group_brightness";
                    CreateParameters();
                }
                else if (actionParameter == "type_group_toggle")
                {
                    _currentState = "select_group_toggle";
                    CreateParameters();
                }
                else if (actionParameter == "type_plugin_action")
                {
                    _currentState = "select_plugin_action";
                    CreateParameters();
                }
                return;
            }

            // Device selection
            if (_currentState == "select_device")
            {
                if (actionParameter.StartsWith("device_"))
                {
                    var deviceId = actionParameter.Substring(7);
                    AddDeviceToggleButton(deviceId);
                }
                return;
            }

            // Color selection
            if (_currentState == "select_group_color")
            {
                if (actionParameter.StartsWith("color_"))
                {
                    var parts = actionParameter.Split('_');
                    if (parts.Length >= 3)
                    {
                        var color = parts[1];
                        var groupId = parts[2];
                        AddGroupColorButton(groupId, color);
                    }
                }
                return;
            }

            // Brightness selection
            if (_currentState == "select_group_brightness")
            {
                if (actionParameter.StartsWith("bright_"))
                {
                    var parts = actionParameter.Split('_');
                    if (parts.Length >= 3)
                    {
                        var brightness = parts[1];
                        var groupId = parts[2];
                        AddGroupBrightnessButton(groupId, brightness);
                    }
                }
                return;
            }

            // Group toggle selection
            if (_currentState == "select_group_toggle")
            {
                if (actionParameter.StartsWith("grouptoggle_"))
                {
                    var groupId = actionParameter.Substring(12);
                    AddGroupToggleButton(groupId);
                }
                return;
            }

            // Plugin action selection
            if (_currentState == "select_plugin_action")
            {
                if (actionParameter.StartsWith("action_"))
                {
                    var parts = actionParameter.Substring(7).Split('_'); // Remove "action_" prefix
                    if (parts.Length >= 2)
                    {
                        var actionName = parts[0];
                        var actionParam = parts.Length > 1 ? parts[1] : "";
                        AddGenericActionButton(actionName, actionParam);
                    }
                }
                return;
            }
        }

        private void CreateNewFolder()
        {
            var folder = new FolderConfiguration
            {
                Name = $"Folder {_plugin.Folders.Count + 1}",
                Buttons = new List<FolderButton>()
            };

            _plugin.AddFolder(folder);
            _selectedFolderId = folder.Id;
            _currentState = $"edit_{folder.Id}";
            CreateParameters();

            System.Windows.Forms.MessageBox.Show(
                $"New folder '{folder.Name}' created!\n\nAdd buttons to customize it.",
                "Folder Created",
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Information
            );
        }

        private void AddDeviceToggleButton(string deviceId)
        {
            var folder = _plugin.Folders.FirstOrDefault(f => f.Id == _selectedFolderId);
            var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);

            if (folder != null && device != null)
            {
                folder.Buttons.Add(new FolderButton(FolderButtonType.DeviceToggle, deviceId, "ch0", device.Name));
                _plugin.UpdateFolder(folder);

                _currentState = $"edit_{_selectedFolderId}";
                CreateParameters();
            }
        }

        private void AddGroupColorButton(string groupId, string color)
        {
            var folder = _plugin.Folders.FirstOrDefault(f => f.Id == _selectedFolderId);
            var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);

            if (folder != null && group != null)
            {
                folder.Buttons.Add(new FolderButton(FolderButtonType.GroupColor, groupId, color, color.ToUpper()));
                _plugin.UpdateFolder(folder);

                _currentState = $"edit_{_selectedFolderId}";
                CreateParameters();
            }
        }

        private void AddGroupBrightnessButton(string groupId, string brightness)
        {
            var folder = _plugin.Folders.FirstOrDefault(f => f.Id == _selectedFolderId);
            var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);

            if (folder != null && group != null)
            {
                folder.Buttons.Add(new FolderButton(FolderButtonType.GroupBrightness, groupId, brightness, $"{brightness}%"));
                _plugin.UpdateFolder(folder);

                _currentState = $"edit_{_selectedFolderId}";
                CreateParameters();
            }
        }

        private void AddGroupToggleButton(string groupId)
        {
            var folder = _plugin.Folders.FirstOrDefault(f => f.Id == _selectedFolderId);
            var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);

            if (folder != null && group != null)
            {
                folder.Buttons.Add(new FolderButton(FolderButtonType.GroupToggle, groupId, null, $"{group.Name} ALL"));
                _plugin.UpdateFolder(folder);

                _currentState = $"edit_{_selectedFolderId}";
                CreateParameters();
            }
        }

        private void AddGenericActionButton(string actionName, string actionParameter)
        {
            var folder = _plugin.Folders.FirstOrDefault(f => f.Id == _selectedFolderId);

            if (folder != null)
            {
                // Generate display label based on action and parameter
                string label = actionName;

                // Find device name if applicable
                var device = _plugin.Devices.FirstOrDefault(d => d.Id == actionParameter);
                if (device != null)
                {
                    label = $"{device.Name} - {actionName.Replace("Action", "").Replace("Command", "").Replace("Adjustment", "")}";
                }
                else if (string.IsNullOrEmpty(actionParameter))
                {
                    label = actionName.Replace("Action", "").Replace("Command", "").Replace("Adjustment", "");
                }

                folder.Buttons.Add(new FolderButton(actionName, actionParameter, label));
                _plugin.UpdateFolder(folder);

                _currentState = $"edit_{_selectedFolderId}";
                CreateParameters();
            }
        }

        private void RemoveLastButton()
        {
            var folder = _plugin.Folders.FirstOrDefault(f => f.Id == _selectedFolderId);

            if (folder != null && folder.Buttons.Count > 0)
            {
                folder.Buttons.RemoveAt(folder.Buttons.Count - 1);
                _plugin.UpdateFolder(folder);
                CreateParameters();
            }
        }

        private void DeleteFolder()
        {
            var result = System.Windows.Forms.MessageBox.Show(
                "Are you sure you want to delete this folder?",
                "Confirm Delete",
                System.Windows.Forms.MessageBoxButtons.YesNo,
                System.Windows.Forms.MessageBoxIcon.Warning
            );

            if (result == System.Windows.Forms.DialogResult.Yes)
            {
                _plugin.RemoveFolder(_selectedFolderId);
                _currentState = null;
                _selectedFolderId = null;
                CreateParameters();
            }
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var builder = new BitmapBuilder(imageSize))
            {
                builder.Clear(new BitmapColor(70, 130, 180));

                if (string.IsNullOrEmpty(actionParameter))
                {
                    builder.DrawText("Folder", BitmapColor.White, 14);
                }
                else if (actionParameter == "create_new")
                {
                    builder.Clear(new BitmapColor(50, 150, 50));
                    builder.DrawText("NEW", BitmapColor.White, 16);
                }
                else if (actionParameter.StartsWith("edit_"))
                {
                    builder.Clear(new BitmapColor(180, 130, 70));
                    builder.DrawText("EDIT", BitmapColor.White, 16);
                }
                else if (actionParameter == "add_button")
                {
                    builder.Clear(new BitmapColor(50, 150, 50));
                    builder.DrawText("➕", BitmapColor.White, 30);
                }
                else if (actionParameter == "remove_last")
                {
                    builder.Clear(new BitmapColor(200, 100, 100));
                    builder.DrawText("➖", BitmapColor.White, 30);
                }
                else if (actionParameter == "delete_folder")
                {
                    builder.Clear(new BitmapColor(200, 50, 50));
                    builder.DrawText("🗑", BitmapColor.White, 30);
                }
                else if (actionParameter == "back")
                {
                    builder.Clear(new BitmapColor(100, 100, 100));
                    builder.DrawText("⬅", BitmapColor.White, 30);
                }
                else if (actionParameter.StartsWith("type_"))
                {
                    builder.Clear(new BitmapColor(100, 100, 200));
                    if (actionParameter.Contains("device"))
                        builder.DrawText("Device", BitmapColor.White, 12);
                    else if (actionParameter.Contains("color"))
                        builder.DrawText("Color", BitmapColor.White, 14);
                    else if (actionParameter.Contains("brightness"))
                        builder.DrawText("Bright", BitmapColor.White, 14);
                    else if (actionParameter.Contains("toggle"))
                        builder.DrawText("Toggle", BitmapColor.White, 14);
                }
                else if (actionParameter.StartsWith("device_"))
                {
                    builder.Clear(new BitmapColor(100, 150, 100));
                    builder.DrawText("DEV", BitmapColor.White, 16);
                }
                else if (actionParameter.StartsWith("color_"))
                {
                    var parts = actionParameter.Split('_');
                    if (parts.Length >= 2)
                    {
                        var color = parts[1];
                        var colorDict = new Dictionary<string, BitmapColor>
                        {
                            { "red", new BitmapColor(255, 0, 0) },
                            { "green", new BitmapColor(0, 255, 0) },
                            { "blue", new BitmapColor(0, 0, 255) },
                            { "white", new BitmapColor(255, 255, 255) },
                            { "yellow", new BitmapColor(255, 255, 0) },
                            { "cyan", new BitmapColor(0, 255, 255) },
                            { "magenta", new BitmapColor(255, 0, 255) }
                        };

                        if (colorDict.ContainsKey(color))
                        {
                            builder.Clear(colorDict[color]);
                            var textColor = (color == "white" || color == "yellow" || color == "cyan") ? BitmapColor.Black : BitmapColor.White;
                            builder.DrawText(color.ToUpper(), textColor, 12);
                        }
                    }
                }
                else if (actionParameter.StartsWith("bright_"))
                {
                    var parts = actionParameter.Split('_');
                    if (parts.Length >= 2 && int.TryParse(parts[1], out int brightness))
                    {
                        var gray = (byte)(brightness * 2.55);
                        builder.Clear(new BitmapColor(gray, gray, gray));
                        builder.DrawText($"{brightness}%", brightness > 50 ? BitmapColor.Black : BitmapColor.White, 18);
                    }
                }
                else if (actionParameter.StartsWith("grouptoggle_"))
                {
                    builder.Clear(new BitmapColor(0, 150, 200));
                    builder.DrawText("ALL", BitmapColor.White, 18);
                }

                return builder.ToImage();
            }
        }
    }
}
