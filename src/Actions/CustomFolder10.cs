using System;
using System.Collections.Generic;
using System.Linq;
using Loupedeck;
using ShellyLoupedeckPlugin.Models;

namespace ShellyLoupedeckPlugin.Actions
{
    public class CustomFolder10 : PluginDynamicFolder
    {
        private const int SLOT_INDEX = 9;
        private ShellyLoupedeckPlugin _plugin;

        // Navigation state
        // null = main menu (device list)
        // "device_{deviceId}" = device settings menu
        // "brightness_{deviceId}" = brightness adjustment
        // "dim_{deviceId}" = dimmer adjustment
        // "color_{deviceId}" = color selection (R, G, B)
        // "color_r_{deviceId}" = red adjustment
        // "color_g_{deviceId}" = green adjustment
        // "color_b_{deviceId}" = blue adjustment
        // "temperature_{deviceId}" = temperature adjustment
        private string _currentSubmenu = null;

        public CustomFolder10()
        {
            DisplayName = "Custom Folder 10";
            Description = "Configurable folder slot 1";
        }

        public override bool Load()
        {
            _plugin = (ShellyLoupedeckPlugin)Plugin;
            _plugin.DevicesUpdated += OnDevicesUpdated;
            _plugin.FoldersUpdated += OnFoldersUpdated;
            UpdateDisplayName();
            return true;
        }

        public override bool Unload()
        {
            _plugin.DevicesUpdated -= OnDevicesUpdated;
            _plugin.FoldersUpdated -= OnFoldersUpdated;
            return true;
        }

        private void OnDevicesUpdated(object sender, EventArgs e)
        {
            // Folder contents will be refreshed when folder is next opened
        }

        private void OnFoldersUpdated(object sender, EventArgs e)
        {
            UpdateDisplayName();
        }

        private void UpdateDisplayName()
        {
            var folder = GetAssignedFolder();
            if (folder != null)
                DisplayName = folder.Name;
            else
                DisplayName = "Custom Folder 10 (Empty)";
        }

        private FolderConfiguration GetAssignedFolder()
        {
            if (_plugin.Folders.Count > SLOT_INDEX)
                return _plugin.Folders[SLOT_INDEX];
            return null;
        }

        public override IEnumerable<string> GetButtonPressActionNames()
        {
            var actions = new List<string> { PluginDynamicFolder.NavigateUpActionName };

            if (!string.IsNullOrEmpty(_currentSubmenu))
            {
                var submenuParts = _currentSubmenu.Split('_');
                var submenuType = submenuParts[0];

                // Level 4: RGB Adjustment (color_r, color_g, color_b)
                if (submenuType == "color" && submenuParts.Length >= 3)
                {
                    var colorChannel = submenuParts[1]; // r, g, or b
                    var deviceId = submenuParts[2];

                    if (colorChannel == "r" || colorChannel == "g" || colorChannel == "b")
                    {
                        // RGB adjustment menu
                        actions.Add(CreateCommandName($"adjust_color_{colorChannel}_{deviceId}_-50"));
                        actions.Add(CreateCommandName($"adjust_color_{colorChannel}_{deviceId}_-20"));
                        actions.Add(CreateCommandName($"adjust_color_{colorChannel}_{deviceId}_-5"));
                        actions.Add(CreateCommandName($"display_color_{colorChannel}_{deviceId}"));
                        actions.Add(CreateCommandName($"adjust_color_{colorChannel}_{deviceId}_+5"));
                        actions.Add(CreateCommandName($"adjust_color_{colorChannel}_{deviceId}_+20"));
                        actions.Add(CreateCommandName($"adjust_color_{colorChannel}_{deviceId}_+50"));
                        return actions;
                    }

                    // Level 3b: Color channel selection
                    deviceId = submenuParts[1];
                    actions.Add(CreateCommandName($"colormenu_r_{deviceId}"));
                    actions.Add(CreateCommandName($"colormenu_g_{deviceId}"));
                    actions.Add(CreateCommandName($"colormenu_b_{deviceId}"));
                    return actions;
                }

                // Level 3a: Brightness/Dim/Temperature Adjustment
                if (submenuParts.Length >= 2)
                {
                    var deviceId = submenuParts[1];

                    switch (submenuType)
                    {
                        case "brightness":
                            actions.Add(CreateCommandName($"adjust_brightness_{deviceId}_-10"));
                            actions.Add(CreateCommandName($"adjust_brightness_{deviceId}_-5"));
                            actions.Add(CreateCommandName($"adjust_brightness_{deviceId}_-1"));
                            actions.Add(CreateCommandName($"display_brightness_{deviceId}"));
                            actions.Add(CreateCommandName($"adjust_brightness_{deviceId}_+1"));
                            actions.Add(CreateCommandName($"adjust_brightness_{deviceId}_+5"));
                            actions.Add(CreateCommandName($"adjust_brightness_{deviceId}_+10"));
                            return actions;

                        case "dim":
                            actions.Add(CreateCommandName($"adjust_dim_{deviceId}_-10"));
                            actions.Add(CreateCommandName($"adjust_dim_{deviceId}_-5"));
                            actions.Add(CreateCommandName($"adjust_dim_{deviceId}_-1"));
                            actions.Add(CreateCommandName($"display_dim_{deviceId}"));
                            actions.Add(CreateCommandName($"adjust_dim_{deviceId}_+1"));
                            actions.Add(CreateCommandName($"adjust_dim_{deviceId}_+5"));
                            actions.Add(CreateCommandName($"adjust_dim_{deviceId}_+10"));
                            return actions;

                        case "temperature":
                            actions.Add(CreateCommandName($"adjust_temperature_{deviceId}_-2"));
                            actions.Add(CreateCommandName($"adjust_temperature_{deviceId}_-1"));
                            actions.Add(CreateCommandName($"adjust_temperature_{deviceId}_-0.5"));
                            actions.Add(CreateCommandName($"display_temperature_{deviceId}"));
                            actions.Add(CreateCommandName($"adjust_temperature_{deviceId}_+0.5"));
                            actions.Add(CreateCommandName($"adjust_temperature_{deviceId}_+1"));
                            actions.Add(CreateCommandName($"adjust_temperature_{deviceId}_+2"));
                            return actions;

                        case "device":
                            // Level 2: Device settings menu
                            var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
                            if (device != null)
                            {
                                var deviceType = device.GetDeviceType();

                                // Show relevant options based on device type
                                if (deviceType == ShellyDeviceType.RGBW)
                                {
                                    actions.Add(CreateCommandName($"devicesetting_brightness_{deviceId}"));
                                    actions.Add(CreateCommandName($"devicesetting_color_{deviceId}"));
                                }
                                else if (deviceType == ShellyDeviceType.Dimmer)
                                {
                                    actions.Add(CreateCommandName($"devicesetting_dim_{deviceId}"));
                                }
                                else if (deviceType == ShellyDeviceType.Thermostat)
                                {
                                    actions.Add(CreateCommandName($"devicesetting_temperature_{deviceId}"));
                                }

                                // Add toggle button for all devices
                                actions.Add(CreateCommandName($"toggle_{deviceId}_ch0"));
                            }
                            return actions;
                    }
                }

                return actions;
            }

            // Level 1: Main menu - show devices from folder config
            var folder = GetAssignedFolder();
            if (folder == null)
                return actions;

            foreach (var button in folder.Buttons)
            {
                string commandName = null;

                switch (button.Type)
                {
                    case FolderButtonType.DeviceToggle:
                        // Device buttons open device settings menu
                        commandName = $"opendevice_{button.TargetId}";
                        break;

                    case FolderButtonType.GroupColor:
                        commandName = $"groupcolor_{button.TargetId}_{button.Parameter}";
                        break;

                    case FolderButtonType.GroupBrightness:
                        commandName = $"groupbrightness_{button.TargetId}_{button.Parameter}";
                        break;

                    case FolderButtonType.GroupTemperature:
                        commandName = $"grouptemp_{button.TargetId}_{button.Parameter}";
                        break;

                    case FolderButtonType.GroupToggle:
                        commandName = $"grouptoggle_{button.TargetId}";
                        break;

                    case FolderButtonType.GenericAction:
                        // Generic actions work directly (but not adjustments)
                        commandName = $"generic_{button.ActionName}_{button.ActionParameter}";
                        break;
                }

                if (commandName != null)
                    actions.Add(CreateCommandName(commandName));
            }

            return actions;
        }

        public override string GetCommandDisplayName(string actionParameter, PluginImageSize imageSize)
        {
            if (actionParameter == PluginDynamicFolder.NavigateUpActionName)
                return "Back";

            var cmdParts = actionParameter.Split('_');

            // Device menu options
            if (cmdParts[0] == "devicesetting" && cmdParts.Length >= 3)
            {
                var settingType = cmdParts[1];
                switch (settingType)
                {
                    case "brightness": return "Brightness";
                    case "dim": return "Dimmer";
                    case "color": return "Color";
                    case "temperature": return "Temperature";
                }
            }

            // Color menu (R, G, B selection)
            if (cmdParts[0] == "colormenu" && cmdParts.Length >= 3)
            {
                var channel = cmdParts[1].ToUpper();
                return channel.ToUpper();
            }

            // Display commands - show current value
            if (cmdParts[0] == "display")
            {
                if (cmdParts.Length >= 3)
                {
                    var displayType = cmdParts[1];
                    var deviceId = cmdParts.Length >= 4 ? cmdParts[3] : cmdParts[2];
                    var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);

                    if (device != null)
                    {
                        if (displayType == "brightness")
                        {
                            int currentBrightness = GetDeviceBrightness(device);
                            return $"{currentBrightness}%";
                        }
                        if (displayType == "dim")
                        {
                            int currentDim = GetDeviceDimmer(device);
                            return $"{currentDim}%";
                        }
                        if (displayType == "temperature")
                        {
                            double currentTemp = GetDeviceTemperature(device);
                            return $"{currentTemp:F1}°C";
                        }
                        if (displayType == "color" && cmdParts.Length >= 4)
                        {
                            var channel = cmdParts[2];
                            var color = GetDeviceColor(device);
                            if (channel == "r") return $"R: {color.R}";
                            if (channel == "g") return $"G: {color.G}";
                            if (channel == "b") return $"B: {color.B}";
                        }
                    }
                }
                return "---";
            }

            // Adjust commands
            if (cmdParts[0] == "adjust")
            {
                if (cmdParts.Length >= 4)
                {
                    var adjustType = cmdParts[1];
                    var adjustment = cmdParts.Length >= 4 ? cmdParts[3] : "";

                    if (adjustType == "brightness" || adjustType == "dim")
                        return $"{adjustment}%";
                    if (adjustType == "temperature")
                        return $"{adjustment}°C";
                    if (adjustType == "color" && cmdParts.Length >= 5)
                        return adjustment;
                }
            }

            // Open device
            if (cmdParts[0] == "opendevice" && cmdParts.Length >= 2)
            {
                var deviceId = cmdParts[1];
                var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
                return device?.Name ?? deviceId;
            }

            // Toggle
            if (cmdParts[0] == "toggle" && cmdParts.Length >= 2)
            {
                var deviceId = cmdParts[1];
                var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
                return device?.Name ?? "Toggle";
            }

            // Group commands
            if (cmdParts[0] == "groupcolor")
                return cmdParts.Length >= 3 ? cmdParts[2].ToUpper() : "Color";
            if (cmdParts[0] == "groupbrightness")
                return cmdParts.Length >= 3 ? $"{cmdParts[2]}%" : "Brightness";
            if (cmdParts[0] == "grouptemp")
                return cmdParts.Length >= 3 ? $"{cmdParts[2]}°C" : "Temperature";
            if (cmdParts[0] == "grouptoggle")
            {
                var groupId = cmdParts.Length >= 2 ? cmdParts[1] : null;
                var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);
                return group?.Name ?? "Toggle";
            }

            // Generic actions
            if (cmdParts[0] == "generic" && cmdParts.Length >= 2)
            {
                var folder = GetAssignedFolder();
                if (folder != null)
                {
                    var actionName = cmdParts[1];
                    var actionParam = cmdParts.Length >= 3 ? cmdParts[2] : null;
                    var genericButton = folder.Buttons.FirstOrDefault(b =>
                        b.Type == FolderButtonType.GenericAction &&
                        b.ActionName == actionName &&
                        b.ActionParameter == actionParam);

                    if (genericButton != null && !string.IsNullOrEmpty(genericButton.CustomLabel))
                        return genericButton.CustomLabel;

                    // Default display for generic actions
                    if (actionName == "DeviceSwitchAction")
                    {
                        var dev = _plugin.Devices.FirstOrDefault(d => d.Id == actionParam);
                        return dev?.Name ?? "Switch";
                    }
                    if (actionName == "ThermostatBoostAction")
                    {
                        var dev = _plugin.Devices.FirstOrDefault(d => d.Id == actionParam);
                        return dev != null ? $"{dev.Name} Boost" : "Boost";
                    }
                    if (actionName == "RGBWModeToggle")
                    {
                        var dev = _plugin.Devices.FirstOrDefault(d => d.Id == actionParam);
                        return dev != null ? $"{dev.Name} Mode" : "Mode";
                    }
                }
                return cmdParts.Length >= 2 ? cmdParts[1] : "Action";
            }

            return actionParameter;
        }

        public override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var builder = new BitmapBuilder(imageSize))
            {
                builder.Clear(BitmapColor.Black);

                if (actionParameter == PluginDynamicFolder.NavigateUpActionName)
                {
                    builder.DrawText("←", BitmapColor.White, 40);
                    return builder.ToImage();
                }

                var cmdParts = actionParameter.Split('_');
                if (cmdParts.Length < 2)
                {
                    builder.DrawText("?", BitmapColor.White);
                    return builder.ToImage();
                }

                // Device setting buttons
                if (cmdParts[0] == "devicesetting")
                {
                    var settingType = cmdParts[1];
                    switch (settingType)
                    {
                        case "brightness":
                            builder.Clear(new BitmapColor(100, 100, 0));
                            builder.DrawText("☀", BitmapColor.White, 36);
                            break;
                        case "dim":
                            builder.Clear(new BitmapColor(80, 80, 80));
                            builder.DrawText("◐", BitmapColor.White, 36);
                            break;
                        case "color":
                            builder.Clear(new BitmapColor(100, 50, 150));
                            builder.DrawText("🎨", BitmapColor.White, 32);
                            break;
                        case "temperature":
                            builder.Clear(new BitmapColor(150, 80, 0));
                            builder.DrawText("🌡", BitmapColor.White, 32);
                            break;
                    }
                    return builder.ToImage();
                }

                // Color channel selection
                if (cmdParts[0] == "colormenu")
                {
                    var channel = cmdParts[1];
                    switch (channel)
                    {
                        case "r":
                            builder.Clear(new BitmapColor(200, 0, 0));
                            builder.DrawText("R", BitmapColor.White, 40);
                            break;
                        case "g":
                            builder.Clear(new BitmapColor(0, 200, 0));
                            builder.DrawText("G", BitmapColor.White, 40);
                            break;
                        case "b":
                            builder.Clear(new BitmapColor(0, 0, 200));
                            builder.DrawText("B", BitmapColor.White, 40);
                            break;
                    }
                    return builder.ToImage();
                }

                // Display values
                if (cmdParts[0] == "display")
                {
                    var displayType = cmdParts[1];

                    if (displayType == "brightness" && cmdParts.Length >= 3)
                    {
                        var deviceId = cmdParts[2];
                        var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
                        if (device != null)
                        {
                            int currentBrightness = GetDeviceBrightness(device);
                            var grayValue = (byte)(currentBrightness * 2.55);
                            builder.Clear(new BitmapColor(grayValue, grayValue, grayValue));
                            builder.DrawText($"{currentBrightness}%", currentBrightness > 50 ? BitmapColor.Black : BitmapColor.White, 26);
                        }
                        return builder.ToImage();
                    }

                    if (displayType == "dim" && cmdParts.Length >= 3)
                    {
                        var deviceId = cmdParts[2];
                        var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
                        if (device != null)
                        {
                            int currentDim = GetDeviceDimmer(device);
                            var grayValue = (byte)(currentDim * 2.55);
                            builder.Clear(new BitmapColor(grayValue, grayValue, grayValue));
                            builder.DrawText($"{currentDim}%", currentDim > 50 ? BitmapColor.Black : BitmapColor.White, 26);
                        }
                        return builder.ToImage();
                    }

                    if (displayType == "temperature" && cmdParts.Length >= 3)
                    {
                        var deviceId = cmdParts[2];
                        var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
                        if (device != null)
                        {
                            double currentTemp = GetDeviceTemperature(device);
                            builder.Clear(new BitmapColor(100, 60, 0));
                            builder.DrawText($"{currentTemp:F1}°C", BitmapColor.White, 22);
                        }
                        return builder.ToImage();
                    }

                    if (displayType == "color" && cmdParts.Length >= 4)
                    {
                        var channel = cmdParts[2];
                        var deviceId = cmdParts[3];
                        var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
                        if (device != null)
                        {
                            var color = GetDeviceColor(device);
                            int value = 0;
                            BitmapColor bgColor = BitmapColor.Black;

                            if (channel == "r")
                            {
                                value = color.R;
                                bgColor = new BitmapColor((byte)value, 0, 0);
                            }
                            else if (channel == "g")
                            {
                                value = color.G;
                                bgColor = new BitmapColor(0, (byte)value, 0);
                            }
                            else if (channel == "b")
                            {
                                value = color.B;
                                bgColor = new BitmapColor(0, 0, (byte)value);
                            }

                            builder.Clear(bgColor);
                            builder.DrawText($"{value}", value > 128 ? BitmapColor.Black : BitmapColor.White, 28);
                        }
                        return builder.ToImage();
                    }

                    return builder.ToImage();
                }

                // Adjust buttons
                if (cmdParts[0] == "adjust")
                {
                    var adjustType = cmdParts[1];

                    if ((adjustType == "brightness" || adjustType == "dim") && cmdParts.Length >= 4)
                    {
                        var adjustment = cmdParts[3];
                        var isPositive = adjustment.StartsWith("+");
                        builder.Clear(isPositive ? new BitmapColor(0, 100, 0) : new BitmapColor(100, 0, 0));
                        builder.DrawText($"{adjustment}%", BitmapColor.White, 24);
                        return builder.ToImage();
                    }

                    if (adjustType == "temperature" && cmdParts.Length >= 4)
                    {
                        var adjustment = cmdParts[3];
                        var isPositive = adjustment.StartsWith("+");
                        builder.Clear(isPositive ? new BitmapColor(150, 80, 0) : new BitmapColor(0, 80, 150));
                        builder.DrawText($"{adjustment}°C", BitmapColor.White, 22);
                        return builder.ToImage();
                    }

                    if (adjustType == "color" && cmdParts.Length >= 5)
                    {
                        var channel = cmdParts[2];
                        var adjustment = cmdParts[4];
                        var isPositive = adjustment.StartsWith("+");

                        BitmapColor bgColor = BitmapColor.Black;
                        if (channel == "r")
                            bgColor = isPositive ? new BitmapColor(100, 0, 0) : new BitmapColor(50, 0, 0);
                        else if (channel == "g")
                            bgColor = isPositive ? new BitmapColor(0, 100, 0) : new BitmapColor(0, 50, 0);
                        else if (channel == "b")
                            bgColor = isPositive ? new BitmapColor(0, 0, 100) : new BitmapColor(0, 0, 50);

                        builder.Clear(bgColor);
                        builder.DrawText(adjustment, BitmapColor.White, 24);
                        return builder.ToImage();
                    }
                }

                // Open device button
                if (cmdParts[0] == "opendevice" && cmdParts.Length >= 2)
                {
                    var deviceId = cmdParts[1];
                    var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
                    if (device != null)
                    {
                        bool isOn = GetDeviceState(device, 0);
                        builder.Clear(isOn ? new BitmapColor(0, 150, 0) : new BitmapColor(60, 60, 60));
                        builder.DrawText(device.Name ?? "Device", BitmapColor.White, 18);
                    }
                    return builder.ToImage();
                }

                // Toggle button
                if (cmdParts[0] == "toggle" && cmdParts.Length >= 3)
                {
                    var deviceId = cmdParts[1];
                    var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
                    if (device != null)
                    {
                        bool isOn = GetDeviceState(device, 0);
                        builder.Clear(isOn ? new BitmapColor(0, 200, 0) : new BitmapColor(80, 80, 80));
                        builder.DrawText(isOn ? "ON" : "OFF", BitmapColor.White, 30);
                    }
                    return builder.ToImage();
                }

                // Group commands
                if (cmdParts[0] == "groupcolor" && cmdParts.Length >= 3)
                {
                    var colorKey = cmdParts[2];
                    var colorPresets = new Dictionary<string, (int, int, int)>
                    {
                        { "red", (255, 0, 0) },
                        { "green", (0, 255, 0) },
                        { "blue", (0, 0, 255) },
                        { "white", (255, 255, 255) },
                        { "yellow", (255, 255, 0) },
                        { "cyan", (0, 255, 255) },
                        { "magenta", (255, 0, 255) }
                    };

                    if (colorPresets.ContainsKey(colorKey))
                    {
                        var color = colorPresets[colorKey];
                        builder.Clear(new BitmapColor((byte)color.Item1, (byte)color.Item2, (byte)color.Item3));
                        var brightness = (color.Item1 + color.Item2 + color.Item3) / 3;
                        var textColor = brightness > 128 ? BitmapColor.Black : BitmapColor.White;
                        builder.DrawText(colorKey.ToUpper(), textColor, 14);
                    }
                    return builder.ToImage();
                }

                if (cmdParts[0] == "groupbrightness" && cmdParts.Length >= 3)
                {
                    if (int.TryParse(cmdParts[2], out int bright))
                    {
                        var grayValue = (byte)(bright * 2.55);
                        builder.Clear(new BitmapColor(grayValue, grayValue, grayValue));
                        builder.DrawText($"{bright}%", bright > 50 ? BitmapColor.Black : BitmapColor.White, 22);
                    }
                    return builder.ToImage();
                }

                if (cmdParts[0] == "grouptemp" && cmdParts.Length >= 3)
                {
                    builder.Clear(BitmapColor.Black);
                    builder.DrawText($"{cmdParts[2]}°C", BitmapColor.White, 20);
                    return builder.ToImage();
                }

                if (cmdParts[0] == "grouptoggle" && cmdParts.Length >= 2)
                {
                    builder.Clear(new BitmapColor(0, 150, 200));
                    builder.DrawText("ALL", BitmapColor.White, 28);
                    return builder.ToImage();
                }

                // Generic actions
                if (cmdParts[0] == "generic" && cmdParts.Length >= 2)
                {
                    var actionName = cmdParts[1];
                    var actionParam = cmdParts.Length >= 3 ? cmdParts[2] : null;

                    if (actionName == "DeviceSwitchAction" && actionParam != null)
                    {
                        var device = _plugin.Devices.FirstOrDefault(d => d.Id == actionParam);
                        if (device != null)
                        {
                            var isOn = GetDeviceState(device, 0);
                            builder.Clear(isOn ? new BitmapColor(0, 200, 0) : new BitmapColor(80, 80, 80));
                            builder.DrawText(isOn ? "ON" : "OFF", BitmapColor.White, 30);
                        }
                    }
                    else if (actionName == "ThermostatBoostAction")
                    {
                        builder.Clear(new BitmapColor(200, 100, 0));
                        builder.DrawText("BOOST", BitmapColor.White, 22);
                    }
                    else if (actionName == "RGBWModeToggle")
                    {
                        builder.Clear(new BitmapColor(150, 0, 200));
                        builder.DrawText("MODE", BitmapColor.White, 24);
                    }
                    else
                    {
                        builder.Clear(BitmapColor.Black);
                        builder.DrawText(actionName, BitmapColor.White, 10);
                    }
                    return builder.ToImage();
                }

                return builder.ToImage();
            }
        }

        public override void RunCommand(string actionParameter)
        {
            DebugLogger.Log($"[CustomFolder10] RunCommand called: {actionParameter}");
            DebugLogger.Log($"[CustomFolder10] Current submenu: {_currentSubmenu ?? "null"}");

            if (actionParameter == PluginDynamicFolder.NavigateUpActionName)
            {
                DebugLogger.Log("[CustomFolder10] Navigate Up pressed");
                // Back button: go up one level
                if (!string.IsNullOrEmpty(_currentSubmenu))
                {
                    var parts = _currentSubmenu.Split('_');

                    // From level 4 (color_r/g/b_deviceId) -> level 3b (color_deviceId)
                    if (parts[0] == "color" && parts.Length >= 3 && (parts[1] == "r" || parts[1] == "g" || parts[1] == "b"))
                    {
                        _currentSubmenu = $"color_{parts[2]}";
                    }
                    // From level 3 (any adjustment) -> level 2 (device settings)
                    else if (parts.Length >= 2)
                    {
                        _currentSubmenu = $"device_{parts[1]}";
                    }
                    // From level 2 (device settings) -> level 1 (main)
                    else
                    {
                        _currentSubmenu = null;
                    }

                    DebugLogger.Log($"[CustomFolder10] After Navigate Up, submenu: {_currentSubmenu ?? "null"}");

                    // Force folder refresh
                    try
                    {
                        DebugLogger.Log($"[CustomFolder10] Calling ButtonActionNamesChanged() after navigate up");
                        ButtonActionNamesChanged();
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.Log($"[CustomFolder10] ButtonActionNamesChanged failed: {ex.Message}");
                    }
                }
                return;
            }

            var cmdParts = actionParameter.Split('_');
            DebugLogger.Log($"[CustomFolder10] Command parts: {string.Join(", ", cmdParts)}");
            if (cmdParts.Length < 2) return;

            _plugin.RecordUserAction();

            // Open device settings
            if (cmdParts[0] == "opendevice" && cmdParts.Length >= 2)
            {
                DebugLogger.Log($"[CustomFolder10] Opening device settings for: {cmdParts[1]}");
                _currentSubmenu = $"device_{cmdParts[1]}";
                DebugLogger.Log($"[CustomFolder10] Set submenu to: {_currentSubmenu}");

                // Force folder refresh by notifying button list changed
                try
                {
                    DebugLogger.Log($"[CustomFolder10] Calling ButtonActionNamesChanged()");
                    ButtonActionNamesChanged();
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"[CustomFolder10] ButtonActionNamesChanged failed: {ex.Message}");
                }

                return;
            }

            // Open specific setting menu
            if (cmdParts[0] == "devicesetting" && cmdParts.Length >= 3)
            {
                var settingType = cmdParts[1];
                var deviceId = cmdParts[2];
                DebugLogger.Log($"[CustomFolder10] Opening {settingType} menu for: {deviceId}");
                _currentSubmenu = $"{settingType}_{deviceId}";
                DebugLogger.Log($"[CustomFolder10] Set submenu to: {_currentSubmenu}");

                // Force folder refresh
                try
                {
                    DebugLogger.Log($"[CustomFolder10] Calling ButtonActionNamesChanged()");
                    ButtonActionNamesChanged();
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"[CustomFolder10] ButtonActionNamesChanged failed: {ex.Message}");
                }

                return;
            }

            // Open color channel menu
            if (cmdParts[0] == "colormenu" && cmdParts.Length >= 3)
            {
                var channel = cmdParts[1];
                var deviceId = cmdParts[2];
                DebugLogger.Log($"[CustomFolder10] Opening color {channel} menu for: {deviceId}");
                _currentSubmenu = $"color_{channel}_{deviceId}";
                DebugLogger.Log($"[CustomFolder10] Set submenu to: {_currentSubmenu}");

                // Force folder refresh
                try
                {
                    DebugLogger.Log($"[CustomFolder10] Calling ButtonActionNamesChanged()");
                    ButtonActionNamesChanged();
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"[CustomFolder10] ButtonActionNamesChanged failed: {ex.Message}");
                }

                return;
            }

            // Display buttons (do nothing)
            if (cmdParts[0] == "display")
                return;

            // Adjust brightness
            if (cmdParts[0] == "adjust" && cmdParts[1] == "brightness" && cmdParts.Length >= 4)
            {
                var deviceId = cmdParts[2];
                var adjustment = cmdParts[3];
                if (double.TryParse(adjustment, out double adjustValue))
                {
                    _ = AdjustDeviceBrightnessAsync(deviceId, (int)adjustValue);
                }
                return;
            }

            // Adjust dimmer
            if (cmdParts[0] == "adjust" && cmdParts[1] == "dim" && cmdParts.Length >= 4)
            {
                var deviceId = cmdParts[2];
                var adjustment = cmdParts[3];
                if (double.TryParse(adjustment, out double adjustValue))
                {
                    _ = AdjustDeviceDimmerAsync(deviceId, (int)adjustValue);
                }
                return;
            }

            // Adjust temperature
            if (cmdParts[0] == "adjust" && cmdParts[1] == "temperature" && cmdParts.Length >= 4)
            {
                var deviceId = cmdParts[2];
                var adjustment = cmdParts[3];
                if (double.TryParse(adjustment, out double adjustValue))
                {
                    _ = AdjustDeviceTemperatureAsync(deviceId, adjustValue);
                }
                return;
            }

            // Adjust color (R/G/B)
            if (cmdParts[0] == "adjust" && cmdParts[1] == "color" && cmdParts.Length >= 5)
            {
                var channel = cmdParts[2];
                var deviceId = cmdParts[3];
                var adjustment = cmdParts[4];
                if (int.TryParse(adjustment, out int adjustValue))
                {
                    _ = AdjustDeviceColorChannelAsync(deviceId, channel, adjustValue);
                }
                return;
            }

            // Toggle device
            if (cmdParts[0] == "toggle" && cmdParts.Length >= 3)
            {
                var deviceId = cmdParts[1];
                var channelParam = cmdParts[2];
                int channel = 0;
                if (channelParam.StartsWith("ch"))
                    int.TryParse(channelParam.Substring(2), out channel);

                _ = ToggleDeviceAsync(deviceId, channel);
                return;
            }

            // Group commands
            if (cmdParts[0] == "groupcolor" && cmdParts.Length >= 3)
            {
                var groupId = cmdParts[1];
                var colorKey = cmdParts[2];
                var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);

                var colorPresets = new Dictionary<string, (int, int, int, int)>
                {
                    { "red", (255, 0, 0, 0) },
                    { "green", (0, 255, 0, 0) },
                    { "blue", (0, 0, 255, 0) },
                    { "white", (0, 0, 0, 255) },
                    { "yellow", (255, 255, 0, 0) },
                    { "cyan", (0, 255, 255, 0) },
                    { "magenta", (255, 0, 255, 0) }
                };

                if (group != null && colorPresets.ContainsKey(colorKey))
                    _ = SetGroupColorAsync(group, colorPresets[colorKey]);
                return;
            }

            if (cmdParts[0] == "groupbrightness" && cmdParts.Length >= 3)
            {
                var groupId = cmdParts[1];
                if (int.TryParse(cmdParts[2], out int brightness))
                {
                    var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);
                    if (group != null)
                        _ = SetGroupBrightnessAsync(group, brightness);
                }
                return;
            }

            if (cmdParts[0] == "grouptemp" && cmdParts.Length >= 3)
            {
                var groupId = cmdParts[1];
                if (double.TryParse(cmdParts[2], out double temp))
                {
                    var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);
                    if (group != null)
                        _ = SetGroupTemperatureAsync(group, temp);
                }
                return;
            }

            if (cmdParts[0] == "grouptoggle" && cmdParts.Length >= 2)
            {
                var groupId = cmdParts[1];
                var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);
                if (group != null)
                    _ = ToggleGroupAsync(group);
                return;
            }

            // Generic actions
            if (cmdParts[0] == "generic" && cmdParts.Length >= 2)
            {
                var actionName = cmdParts[1];
                var actionParam = cmdParts.Length >= 3 ? cmdParts[2] : "";

                if (actionName == "DeviceSwitchAction" && !string.IsNullOrEmpty(actionParam))
                {
                    _ = ToggleDeviceAsync(actionParam, 0);
                }
            }
        }

        private async System.Threading.Tasks.Task ToggleDeviceAsync(string deviceId, int channel)
        {
            var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
            if (device == null) return;

            bool currentState = GetDeviceState(device, channel);
            bool newState = !currentState;

            var deviceType = device.GetDeviceType();

            switch (deviceType)
            {
                case ShellyDeviceType.Switch:
                case ShellyDeviceType.ShellyPlus2PM:
                    await _plugin.ApiClient.SetRelayStateAsync(deviceId, channel, newState);
                    break;
                case ShellyDeviceType.RGBW:
                case ShellyDeviceType.Dimmer:
                    await _plugin.ApiClient.SetLightStateAsync(deviceId, channel, newState);
                    break;
            }
        }

        private async System.Threading.Tasks.Task ToggleGroupAsync(DeviceGroup group)
        {
            for (int i = 0; i < group.DeviceIds.Count; i++)
            {
                var deviceId = group.DeviceIds[i];
                var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);

                if (device != null)
                {
                    _plugin.RecordUserAction();

                    var deviceType = device.GetDeviceType();
                    bool currentState = false;

                    if (deviceType == ShellyDeviceType.RGBW || deviceType == ShellyDeviceType.Dimmer)
                    {
                        if (device.Status?.Lights != null && device.Status.Lights.Count > 0)
                            currentState = device.Status.Lights[0].IsOn;

                        await _plugin.ApiClient.SetLightStateAsync(deviceId, 0, !currentState);
                    }
                    else
                    {
                        if (device.Status?.Relays != null && device.Status.Relays.Count > 0)
                            currentState = device.Status.Relays[0].IsOn;

                        await _plugin.ApiClient.SetRelayStateAsync(deviceId, 0, !currentState);
                    }

                    if (i < group.DeviceIds.Count - 1)
                        await System.Threading.Tasks.Task.Delay(2000);
                }
            }
        }

        private async System.Threading.Tasks.Task SetGroupColorAsync(DeviceGroup group, (int R, int G, int B, int W) color)
        {
            for (int i = 0; i < group.DeviceIds.Count; i++)
            {
                var deviceId = group.DeviceIds[i];
                var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);

                if (device != null && device.GetDeviceType() == ShellyDeviceType.RGBW)
                {
                    _plugin.RecordUserAction();

                    int brightness = 100;
                    if (device.Status?.Lights != null && device.Status.Lights.Count > 0)
                        brightness = device.Status.Lights[0].Brightness;

                    await _plugin.ApiClient.SetLightColorAsync(deviceId, color.R, color.G, color.B, color.W, brightness: brightness);

                    if (i < group.DeviceIds.Count - 1)
                        await System.Threading.Tasks.Task.Delay(2000);
                }
            }
        }

        private async System.Threading.Tasks.Task SetGroupBrightnessAsync(DeviceGroup group, int brightness)
        {
            for (int i = 0; i < group.DeviceIds.Count; i++)
            {
                _plugin.RecordUserAction();
                await _plugin.ApiClient.SetLightBrightnessAsync(group.DeviceIds[i], brightness);

                if (i < group.DeviceIds.Count - 1)
                    await System.Threading.Tasks.Task.Delay(2000);
            }
        }

        private async System.Threading.Tasks.Task SetGroupTemperatureAsync(DeviceGroup group, double temperature)
        {
            for (int i = 0; i < group.DeviceIds.Count; i++)
            {
                _plugin.RecordUserAction();
                await _plugin.ApiClient.SetThermostatTemperatureAsync(group.DeviceIds[i], temperature);

                if (i < group.DeviceIds.Count - 1)
                    await System.Threading.Tasks.Task.Delay(2000);
            }
        }

        private bool GetDeviceState(ShellyDevice device, int channel = 0)
        {
            if (device.Switch0 != null && channel == 0)
                return device.Switch0.Output;

            if (device.Status?.Relays != null && device.Status.Relays.Count > channel)
                return device.Status.Relays[channel].IsOn;
            if (device.Status?.Lights != null && device.Status.Lights.Count > channel)
                return device.Status.Lights[channel].IsOn;

            if (device.Relays != null && device.Relays.Count > channel)
                return device.Relays[channel].IsOn;
            if (device.Lights != null && device.Lights.Count > channel)
                return device.Lights[channel].IsOn;

            return false;
        }

        private int GetDeviceBrightness(ShellyDevice device, int channel = 0)
        {
            if (device.Status?.Lights != null && device.Status.Lights.Count > channel)
                return device.Status.Lights[channel].Brightness;
            if (device.Lights != null && device.Lights.Count > channel)
                return device.Lights[channel].Brightness;
            return 0;
        }

        private int GetDeviceDimmer(ShellyDevice device, int channel = 0)
        {
            // Dimmer uses the same brightness property but on Dimmer-type devices
            return GetDeviceBrightness(device, channel);
        }

        private double GetDeviceTemperature(ShellyDevice device)
        {
            if (device.Status?.Thermostats != null && device.Status.Thermostats.Count > 0)
                return device.Status.Thermostats[0].TargetTemperature?.Value ?? 20.0;
            if (device.Thermostats != null && device.Thermostats.Count > 0)
                return device.Thermostats[0].TargetTemperature?.Value ?? 20.0;
            return 20.0;
        }

        private (int R, int G, int B) GetDeviceColor(ShellyDevice device, int channel = 0)
        {
            if (device.Status?.Lights != null && device.Status.Lights.Count > channel)
            {
                var light = device.Status.Lights[channel];
                return (light.Red, light.Green, light.Blue);
            }
            if (device.Lights != null && device.Lights.Count > channel)
            {
                var light = device.Lights[channel];
                return (light.Red, light.Green, light.Blue);
            }
            return (0, 0, 0);
        }

        private async System.Threading.Tasks.Task AdjustDeviceBrightnessAsync(string deviceId, int adjustment)
        {
            var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
            if (device == null) return;

            int currentBrightness = GetDeviceBrightness(device);
            int newBrightness = Math.Max(0, Math.Min(100, currentBrightness + adjustment));

            await _plugin.ApiClient.SetLightBrightnessAsync(deviceId, newBrightness);

            // Update device status
            await System.Threading.Tasks.Task.Delay(500);
            await RefreshDeviceStatus(deviceId);
        }

        private async System.Threading.Tasks.Task AdjustDeviceDimmerAsync(string deviceId, int adjustment)
        {
            // Dimmer uses same API as brightness
            await AdjustDeviceBrightnessAsync(deviceId, adjustment);
        }

        private async System.Threading.Tasks.Task AdjustDeviceTemperatureAsync(string deviceId, double adjustment)
        {
            var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
            if (device == null) return;

            double currentTemp = GetDeviceTemperature(device);
            double newTemp = Math.Max(5.0, Math.Min(35.0, currentTemp + adjustment));

            await _plugin.ApiClient.SetThermostatTemperatureAsync(deviceId, newTemp);

            await System.Threading.Tasks.Task.Delay(500);
            await RefreshDeviceStatus(deviceId);
        }

        private async System.Threading.Tasks.Task AdjustDeviceColorChannelAsync(string deviceId, string channel, int adjustment)
        {
            var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
            if (device == null) return;

            var currentColor = GetDeviceColor(device);
            int r = currentColor.R;
            int g = currentColor.G;
            int b = currentColor.B;

            switch (channel)
            {
                case "r":
                    r = Math.Max(0, Math.Min(255, r + adjustment));
                    break;
                case "g":
                    g = Math.Max(0, Math.Min(255, g + adjustment));
                    break;
                case "b":
                    b = Math.Max(0, Math.Min(255, b + adjustment));
                    break;
            }

            int brightness = GetDeviceBrightness(device);
            if (brightness == 0) brightness = 100;

            await _plugin.ApiClient.SetLightColorAsync(deviceId, r, g, b, 0, brightness: brightness);

            await System.Threading.Tasks.Task.Delay(500);
            await RefreshDeviceStatus(deviceId);
        }

        private async System.Threading.Tasks.Task RefreshDeviceStatus(string deviceId)
        {
            var updatedDevice = await _plugin.ApiClient.GetDeviceStatusAsync(deviceId);
            if (updatedDevice != null)
            {
                var index = _plugin.Devices.FindIndex(d => d.Id == deviceId);
                if (index >= 0)
                {
                    _plugin.Devices[index] = updatedDevice;
                    // Trigger UI refresh
                    OnDevicesUpdated(this, EventArgs.Empty);
                }
            }
        }
    }
}
