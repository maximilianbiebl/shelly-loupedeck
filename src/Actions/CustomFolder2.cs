using System;
using System.Collections.Generic;
using System.Linq;
using Loupedeck;
using ShellyLoupedeckPlugin.Models;

namespace ShellyLoupedeckPlugin.Actions
{
    public class CustomFolder2 : PluginDynamicFolder
    {
        private const int SLOT_INDEX = 1;
        private ShellyLoupedeckPlugin _plugin;

        // Submenu state: null = main menu, otherwise format: "type_deviceId" (e.g., "brightness_abc123")
        private string _currentSubmenu = null;

        private Dictionary<string, (int R, int G, int B, int W)> _colorPresets = new Dictionary<string, (int, int, int, int)>
        {
            { "red", (255, 0, 0, 0) },
            { "green", (0, 255, 0, 0) },
            { "blue", (0, 0, 255, 0) },
            { "white", (0, 0, 0, 255) },
            { "yellow", (255, 255, 0, 0) },
            { "cyan", (0, 255, 255, 0) },
            { "magenta", (255, 0, 255, 0) }
        };

        public CustomFolder2()
        {
            DisplayName = "Custom Folder 2";
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
                DisplayName = "Custom Folder 1 (Empty)";
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

            // If we're in a submenu, show submenu buttons instead of main folder buttons
            if (!string.IsNullOrEmpty(_currentSubmenu))
            {
                var submenuParts = _currentSubmenu.Split('_');
                if (submenuParts.Length >= 2)
                {
                    var submenuType = submenuParts[0];
                    var deviceId = submenuParts[1];

                    switch (submenuType)
                    {
                        case "brightness":
                        case "dimmer":
                            // Minus buttons (left side)
                            actions.Add(CreateCommandName($"adjust_brightness_{deviceId}_-10"));
                            actions.Add(CreateCommandName($"adjust_brightness_{deviceId}_-5"));
                            actions.Add(CreateCommandName($"adjust_brightness_{deviceId}_-1"));
                            // Current value display (center)
                            actions.Add(CreateCommandName($"display_brightness_{deviceId}"));
                            // Plus buttons (right side)
                            actions.Add(CreateCommandName($"adjust_brightness_{deviceId}_+1"));
                            actions.Add(CreateCommandName($"adjust_brightness_{deviceId}_+5"));
                            actions.Add(CreateCommandName($"adjust_brightness_{deviceId}_+10"));
                            break;

                        case "color":
                            // Color presets
                            actions.Add(CreateCommandName($"preset_color_{deviceId}_red"));
                            actions.Add(CreateCommandName($"preset_color_{deviceId}_green"));
                            actions.Add(CreateCommandName($"preset_color_{deviceId}_blue"));
                            actions.Add(CreateCommandName($"preset_color_{deviceId}_yellow"));
                            actions.Add(CreateCommandName($"preset_color_{deviceId}_cyan"));
                            actions.Add(CreateCommandName($"preset_color_{deviceId}_magenta"));
                            actions.Add(CreateCommandName($"preset_color_{deviceId}_white"));
                            break;

                        case "temperature":
                            // Minus buttons (left side)
                            actions.Add(CreateCommandName($"adjust_temperature_{deviceId}_-2"));
                            actions.Add(CreateCommandName($"adjust_temperature_{deviceId}_-1"));
                            actions.Add(CreateCommandName($"adjust_temperature_{deviceId}_-0.5"));
                            // Current value display (center)
                            actions.Add(CreateCommandName($"display_temperature_{deviceId}"));
                            // Plus buttons (right side)
                            actions.Add(CreateCommandName($"adjust_temperature_{deviceId}_+0.5"));
                            actions.Add(CreateCommandName($"adjust_temperature_{deviceId}_+1"));
                            actions.Add(CreateCommandName($"adjust_temperature_{deviceId}_+2"));
                            break;
                    }
                }

                return actions;
            }

            // Normal main menu
            var folder = GetAssignedFolder();
            if (folder == null)
                return actions;

            foreach (var button in folder.Buttons)
            {
                string commandName = null;

                switch (button.Type)
                {
                    case FolderButtonType.DeviceToggle:
                        commandName = $"toggle_{button.TargetId}_{button.Parameter}";
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
                        // Check if this is an adjustment action that should open a submenu
                        if (button.ActionName == "BrightnessAdjustment" || button.ActionName == "DimmerAdjustment")
                        {
                            commandName = $"submenu_brightness_{button.ActionParameter}";
                        }
                        else if (button.ActionName == "ColorAdjustment")
                        {
                            commandName = $"submenu_color_{button.ActionParameter}";
                        }
                        else if (button.ActionName == "TemperatureAdjustment")
                        {
                            commandName = $"submenu_temperature_{button.ActionParameter}";
                        }
                        else
                        {
                            commandName = $"generic_{button.ActionName}_{button.ActionParameter}";
                        }
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

            // actionParameter is already the command name (e.g. "groupcolor_id_red")
            var cmdParts = actionParameter.Split('_');

            // Handle submenu commands
            if (cmdParts[0] == "submenu")
            {
                var deviceId = cmdParts.Length >= 3 ? cmdParts[2] : null;
                var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
                return device?.Name ?? "Adjust";
            }

            // Handle display commands - show current value
            if (cmdParts[0] == "display")
            {
                if (cmdParts.Length >= 3)
                {
                    var deviceId = cmdParts[2];
                    var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
                    if (device != null)
                    {
                        if (cmdParts[1] == "brightness")
                        {
                            int currentBrightness = GetDeviceBrightness(device);
                            return $"{currentBrightness}%";
                        }
                        if (cmdParts[1] == "temperature")
                        {
                            double currentTemp = GetDeviceTemperature(device);
                            return $"{currentTemp:F1}°C";
                        }
                    }
                }
                return "---";
            }

            // Handle preset commands
            if (cmdParts[0] == "preset")
            {
                if (cmdParts[1] == "color" && cmdParts.Length >= 4)
                    return cmdParts[3].ToUpper();
            }

            // Handle adjust commands
            if (cmdParts[0] == "adjust")
            {
                if (cmdParts.Length >= 4)
                {
                    var adjustment = cmdParts[3];
                    if (cmdParts[1] == "brightness")
                        return $"{adjustment}%";
                    if (cmdParts[1] == "temperature")
                        return $"{adjustment}°C";
                }
            }

            // Find button configuration for custom labels
            var folder = GetAssignedFolder();
            if (folder != null)
            {
                var targetId = cmdParts.Length >= 2 ? cmdParts[1] : null;
                var parameter = cmdParts.Length >= 3 ? cmdParts[2] : null;
                var button = folder.Buttons.FirstOrDefault(b => b.TargetId == targetId && b.Parameter == parameter);

                if (button != null && !string.IsNullOrEmpty(button.CustomLabel))
                    return button.CustomLabel;
            }

            // Default display names
            if (cmdParts.Length < 2) return actionParameter;

            switch (cmdParts[0])
            {
                case "toggle":
                    var deviceId = cmdParts.Length >= 2 ? cmdParts[1] : null;
                    var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
                    return device?.Name ?? deviceId;

                case "groupcolor":
                    return cmdParts.Length >= 3 ? cmdParts[2].ToUpper() : "Color";

                case "groupbrightness":
                    return cmdParts.Length >= 3 ? $"{cmdParts[2]}%" : "Brightness";

                case "grouptemp":
                    return cmdParts.Length >= 3 ? $"{cmdParts[2]}°C" : "Temperature";

                case "grouptoggle":
                    var groupId = cmdParts.Length >= 2 ? cmdParts[1] : null;
                    var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);
                    return group?.Name ?? "Toggle";

                case "generic":
                    // For generic actions, use the custom label from folder configuration
                    if (folder != null && cmdParts.Length >= 2)
                    {
                        var actionName = cmdParts.Length >= 2 ? cmdParts[1] : null;
                        var actionParam = cmdParts.Length >= 3 ? cmdParts[2] : null;
                        var genericButton = folder.Buttons.FirstOrDefault(b =>
                            b.Type == FolderButtonType.GenericAction &&
                            b.ActionName == actionName &&
                            b.ActionParameter == actionParam);

                        if (genericButton != null && !string.IsNullOrEmpty(genericButton.CustomLabel))
                            return genericButton.CustomLabel;

                        // Default display for generic actions based on type
                        if (actionName == "DeviceSwitchAction")
                        {
                            var dev = _plugin.Devices.FirstOrDefault(d => d.Id == actionParam);
                            return device?.Name ?? "Switch";
                        }
                        else if (actionName == "ThermostatBoostAction")
                        {
                            var dev = _plugin.Devices.FirstOrDefault(d => d.Id == actionParam);
                            return dev != null ? $"{dev.Name} Boost" : "Boost";
                        }
                        else if (actionName == "RGBWModeToggle")
                        {
                            var dev = _plugin.Devices.FirstOrDefault(d => d.Id == actionParam);
                            return dev != null ? $"{dev.Name} Mode" : "Mode";
                        }
                        else if (actionName.Contains("Adjustment"))
                        {
                            var dev = _plugin.Devices.FirstOrDefault(d => d.Id == actionParam);
                            return device?.Name ?? "Adjust";
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

                // actionParameter is already the command name (e.g. "groupcolor_id_red")
                var cmdParts = actionParameter.Split('_');
                if (cmdParts.Length < 2)
                {
                    builder.DrawText("?", BitmapColor.White);
                    return builder.ToImage();
                }

                // Handle submenu button - show arrow to indicate it opens a submenu
                if (cmdParts[0] == "submenu")
                {
                    builder.Clear(new BitmapColor(50, 50, 100));
                    builder.DrawText("→", BitmapColor.White, 40);
                    return builder.ToImage();
                }

                // Handle display brightness - show current value
                if (cmdParts[0] == "display" && cmdParts[1] == "brightness" && cmdParts.Length >= 3)
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

                // Handle display temperature - show current value
                if (cmdParts[0] == "display" && cmdParts[1] == "temperature" && cmdParts.Length >= 3)
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

                // Handle preset color buttons
                if (cmdParts[0] == "preset" && cmdParts[1] == "color" && cmdParts.Length >= 4)
                {
                    var colorKey = cmdParts[3];
                    if (_colorPresets.ContainsKey(colorKey))
                    {
                        var color = _colorPresets[colorKey];
                        builder.Clear(new BitmapColor((byte)color.R, (byte)color.G, (byte)color.B));
                        var brightness = (color.R + color.G + color.B) / 3;
                        var textColor = brightness > 128 ? BitmapColor.Black : BitmapColor.White;
                        builder.DrawText(colorKey.ToUpper(), textColor, 14);
                    }
                    return builder.ToImage();
                }

                // Handle adjust brightness buttons
                if (cmdParts[0] == "adjust" && cmdParts[1] == "brightness" && cmdParts.Length >= 4)
                {
                    var adjustment = cmdParts[3];
                    var isPositive = adjustment.StartsWith("+");
                    builder.Clear(isPositive ? new BitmapColor(0, 100, 0) : new BitmapColor(100, 0, 0));
                    builder.DrawText($"{adjustment}%", BitmapColor.White, 24);
                    return builder.ToImage();
                }

                // Handle adjust temperature buttons
                if (cmdParts[0] == "adjust" && cmdParts[1] == "temperature" && cmdParts.Length >= 4)
                {
                    var adjustment = cmdParts[3];
                    var isPositive = adjustment.StartsWith("+");
                    builder.Clear(isPositive ? new BitmapColor(150, 80, 0) : new BitmapColor(0, 80, 150));
                    builder.DrawText($"{adjustment}°C", BitmapColor.White, 22);
                    return builder.ToImage();
                }

                switch (cmdParts[0])
                {
                    case "toggle":
                        if (cmdParts.Length >= 3)
                        {
                            var deviceId = cmdParts[1];
                            var channelParam = cmdParts[2];
                            int channel = 0;
                            if (channelParam.StartsWith("ch"))
                                int.TryParse(channelParam.Substring(2), out channel);

                            var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
                            if (device != null)
                            {
                                bool isOn = false;
                                var deviceType = device.GetDeviceType();

                                if (deviceType == ShellyDeviceType.RGBW || deviceType == ShellyDeviceType.Dimmer)
                                {
                                    if (device.Status?.Lights != null && device.Status.Lights.Count > channel)
                                        isOn = device.Status.Lights[channel].IsOn;
                                }
                                else
                                {
                                    if (device.Status?.Relays != null && device.Status.Relays.Count > channel)
                                        isOn = device.Status.Relays[channel].IsOn;
                                }

                                builder.Clear(isOn ? new BitmapColor(0, 200, 0) : new BitmapColor(80, 80, 80));
                                builder.DrawText(isOn ? "ON" : "OFF", BitmapColor.White, 30);
                            }
                        }
                        break;

                    case "groupcolor":
                        if (cmdParts.Length >= 3)
                        {
                            var colorKey = cmdParts[2];
                            if (_colorPresets.ContainsKey(colorKey))
                            {
                                var color = _colorPresets[colorKey];
                                builder.Clear(new BitmapColor((byte)color.R, (byte)color.G, (byte)color.B));

                                var brightness = (color.R + color.G + color.B) / 3;
                                var textColor = brightness > 128 ? BitmapColor.Black : BitmapColor.White;
                                builder.DrawText(colorKey.ToUpper(), textColor, 14);
                            }
                        }
                        break;

                    case "groupbrightness":
                        if (cmdParts.Length >= 3 && int.TryParse(cmdParts[2], out int bright))
                        {
                            var grayValue = (byte)(bright * 2.55);
                            builder.Clear(new BitmapColor(grayValue, grayValue, grayValue));
                            builder.DrawText($"{bright}%", bright > 50 ? BitmapColor.Black : BitmapColor.White, 22);
                        }
                        break;

                    case "grouptemp":
                        if (cmdParts.Length >= 3)
                        {
                            builder.Clear(BitmapColor.Black);
                            builder.DrawText($"{cmdParts[2]}°C", BitmapColor.White, 20);
                        }
                        break;

                    case "grouptoggle":
                        if (cmdParts.Length >= 2)
                        {
                            var groupId = cmdParts[1];
                            var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);
                            if (group != null)
                            {
                                builder.Clear(new BitmapColor(0, 150, 200));
                                builder.DrawText("ALL", BitmapColor.White, 28);
                            }
                        }
                        break;

                    case "generic":
                        if (cmdParts.Length >= 2)
                        {
                            var actionName = cmdParts[1];
                            var actionParam = cmdParts.Length >= 3 ? cmdParts[2] : null;

                            if (actionName == "DeviceSwitchAction" && actionParam != null)
                            {
                                var dev = _plugin.Devices.FirstOrDefault(d => d.Id == actionParam);
                                if (device != null)
                                {
                                    var isOn = GetDeviceState(device, 0);
                                    builder.Clear(isOn ? new BitmapColor(0, 200, 0) : new BitmapColor(80, 80, 80));
                                    builder.DrawText(isOn ? "ON" : "OFF", BitmapColor.White, 30);
                                }
                            }
                            else if (actionName == "ThermostatBoostAction" && actionParam != null)
                            {
                                builder.Clear(new BitmapColor(200, 100, 0));
                                builder.DrawText("BOOST", BitmapColor.White, 22);
                            }
                            else if (actionName == "RGBWModeToggle" && actionParam != null)
                            {
                                builder.Clear(new BitmapColor(150, 0, 200));
                                builder.DrawText("MODE", BitmapColor.White, 24);
                            }
                            else if (actionName.Contains("Adjustment"))
                            {
                                // Adjustments can't work in folders - show N/A
                                builder.Clear(new BitmapColor(60, 60, 60));
                                builder.DrawText("N/A", BitmapColor.White, 22);
                                builder.DrawText("Use main\ninterface", BitmapColor.White, 8);
                            }
                            else
                            {
                                // Unknown generic action
                                builder.Clear(BitmapColor.Black);
                                builder.DrawText(actionName, BitmapColor.White, 10);
                            }
                        }
                        break;
                }

                return builder.ToImage();
            }
        }

        public override void RunCommand(string actionParameter)
        {
            if (actionParameter == PluginDynamicFolder.NavigateUpActionName)
            {
                // Back button: close submenu if in submenu, otherwise do nothing
                if (!string.IsNullOrEmpty(_currentSubmenu))
                {
                    _currentSubmenu = null;
                    // Trigger folder refresh to show main menu again
                    OnDevicesUpdated(this, EventArgs.Empty);
                }
                return;
            }

            // actionParameter is already the command name (e.g. "groupcolor_id_red")
            var cmdParts = actionParameter.Split('_');
            if (cmdParts.Length < 2) return;

            _plugin.RecordUserAction();

            // Handle submenu opening
            if (cmdParts[0] == "submenu" && cmdParts.Length >= 3)
            {
                _currentSubmenu = $"{cmdParts[1]}_{cmdParts[2]}";
                // Trigger folder refresh to show submenu buttons
                OnDevicesUpdated(this, EventArgs.Empty);
                return;
            }

            // Handle display buttons (do nothing, they're just for display)
            if (cmdParts[0] == "display")
                return;

            // Handle adjust brightness
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

            // Handle adjust temperature
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

            // Handle preset color
            if (cmdParts[0] == "preset" && cmdParts[1] == "color" && cmdParts.Length >= 4)
            {
                var deviceId = cmdParts[2];
                var colorKey = cmdParts[3];
                if (_colorPresets.ContainsKey(colorKey))
                {
                    _ = SetDeviceColorAsync(deviceId, _colorPresets[colorKey]);
                }
                return;
            }

            switch (cmdParts[0])
            {
                case "toggle":
                    if (cmdParts.Length >= 3)
                    {
                        var deviceId = cmdParts[1];
                        var channelParam = cmdParts[2];
                        int channel = 0;
                        if (channelParam.StartsWith("ch"))
                            int.TryParse(channelParam.Substring(2), out channel);

                        _ = ToggleDeviceAsync(deviceId, channel);
                    }
                    break;

                case "groupcolor":
                    if (cmdParts.Length >= 3)
                    {
                        var groupId = cmdParts[1];
                        var colorKey = cmdParts[2];
                        var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);
                        if (group != null && _colorPresets.ContainsKey(colorKey))
                            _ = SetGroupColorAsync(group, _colorPresets[colorKey]);
                    }
                    break;

                case "groupbrightness":
                    if (cmdParts.Length >= 3 && int.TryParse(cmdParts[2], out int brightness))
                    {
                        var groupId = cmdParts[1];
                        var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);
                        if (group != null)
                            _ = SetGroupBrightnessAsync(group, brightness);
                    }
                    break;

                case "grouptemp":
                    if (cmdParts.Length >= 3 && double.TryParse(cmdParts[2], out double temp))
                    {
                        var groupId = cmdParts[1];
                        var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);
                        if (group != null)
                            _ = SetGroupTemperatureAsync(group, temp);
                    }
                    break;

                case "grouptoggle":
                    if (cmdParts.Length >= 2)
                    {
                        var groupId = cmdParts[1];
                        var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);
                        if (group != null)
                            _ = ToggleGroupAsync(group);
                    }
                    break;

                case "generic":
                    if (cmdParts.Length >= 2)
                    {
                        var actionName = cmdParts[1];
                        var actionParam = cmdParts.Length >= 3 ? cmdParts[2] : "";

                        // Handle most common generic actions
                        // Note: More complex actions (adjustments, etc.) are not fully supported
                        // as they require UI interaction that can't be automated here
                        if (actionName == "DeviceSwitchAction" && !string.IsNullOrEmpty(actionParam))
                        {
                            _ = ToggleDeviceAsync(actionParam, 0);
                        }
                        // Other actions would need to be triggered through the plugin system
                        // which is not directly accessible here
                    }
                    break;
            }
        }

        private async System.Threading.Tasks.Task ToggleDeviceAsync(string deviceId, int channel)
        {
            var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
            if (device == null) return;

            bool currentState = false;
            var deviceType = device.GetDeviceType();

            if (deviceType == ShellyDeviceType.RGBW || deviceType == ShellyDeviceType.Dimmer)
            {
                if (device.Status?.Lights != null && device.Status.Lights.Count > channel)
                    currentState = device.Status.Lights[channel].IsOn;
            }
            else
            {
                if (device.Status?.Relays != null && device.Status.Relays.Count > channel)
                    currentState = device.Status.Relays[channel].IsOn;
            }

            bool newState = !currentState;

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
            // Check Gen 3 devices first
            if (device.Switch0 != null && channel == 0)
                return device.Switch0.Output;

            // Check Gen 1/2 devices
            if (device.Status?.Relays != null && device.Status.Relays.Count > channel)
                return device.Status.Relays[channel].IsOn;
            if (device.Status?.Lights != null && device.Status.Lights.Count > channel)
                return device.Status.Lights[channel].IsOn;

            // Fallback for Gen 1/2 devices with direct fields
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

        private double GetDeviceTemperature(ShellyDevice device)
        {
            if (device.Status?.Thermostats != null && device.Status.Thermostats.Count > 0)
                return device.Status.Thermostats[0].TargetTemperature?.Value ?? 20.0;
            if (device.Thermostats != null && device.Thermostats.Count > 0)
                return device.Thermostats[0].TargetTemperature?.Value ?? 20.0;
            return 20.0; // Default value
        }

        private async System.Threading.Tasks.Task AdjustDeviceBrightnessAsync(string deviceId, int adjustment)
        {
            var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
            if (device == null) return;

            int currentBrightness = GetDeviceBrightness(device);
            int newBrightness = Math.Max(0, Math.Min(100, currentBrightness + adjustment));

            await _plugin.ApiClient.SetLightBrightnessAsync(deviceId, newBrightness);

            // Update device status after a short delay
            await System.Threading.Tasks.Task.Delay(500);
            var updatedDevice = await _plugin.ApiClient.GetDeviceStatusAsync(deviceId);
            if (updatedDevice != null)
            {
                var index = _plugin.Devices.FindIndex(d => d.Id == deviceId);
                if (index >= 0)
                    _plugin.Devices[index] = updatedDevice;
            }
        }

        private async System.Threading.Tasks.Task AdjustDeviceTemperatureAsync(string deviceId, double adjustment)
        {
            var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
            if (device == null) return;

            double currentTemp = GetDeviceTemperature(device);
            double newTemp = Math.Max(5.0, Math.Min(35.0, currentTemp + adjustment));

            await _plugin.ApiClient.SetThermostatTemperatureAsync(deviceId, newTemp);

            // Update device status after a short delay
            await System.Threading.Tasks.Task.Delay(500);
            var updatedDevice = await _plugin.ApiClient.GetDeviceStatusAsync(deviceId);
            if (updatedDevice != null)
            {
                var index = _plugin.Devices.FindIndex(d => d.Id == deviceId);
                if (index >= 0)
                    _plugin.Devices[index] = updatedDevice;
            }
        }

        private async System.Threading.Tasks.Task SetDeviceColorAsync(string deviceId, (int R, int G, int B, int W) color)
        {
            var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
            if (device == null) return;

            int brightness = GetDeviceBrightness(device);
            if (brightness == 0) brightness = 100; // If off, set to 100% when setting color

            await _plugin.ApiClient.SetLightColorAsync(deviceId, color.R, color.G, color.B, color.W, brightness: brightness);

            // Update device status after a short delay
            await System.Threading.Tasks.Task.Delay(500);
            var updatedDevice = await _plugin.ApiClient.GetDeviceStatusAsync(deviceId);
            if (updatedDevice != null)
            {
                var index = _plugin.Devices.FindIndex(d => d.Id == deviceId);
                if (index >= 0)
                    _plugin.Devices[index] = updatedDevice;
            }
        }
    }
}
