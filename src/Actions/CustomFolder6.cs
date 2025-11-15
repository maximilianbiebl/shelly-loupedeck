using System;
using System.Collections.Generic;
using System.Linq;
using Loupedeck;
using ShellyLoupedeckPlugin.Models;

namespace ShellyLoupedeckPlugin.Actions
{
    public class CustomFolder6 : PluginDynamicFolder
    {
        private const int SLOT_INDEX = 5;
        private ShellyLoupedeckPlugin _plugin;

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

        public CustomFolder6()
        {
            DisplayName = "Custom Folder 6";
            Description = "Configurable folder slot 6";
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
            // Trigger refresh of folder display
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
                DisplayName = "Custom Folder 6 (Empty)";
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

            var parts = actionParameter.Split(new[] { GetType().FullName }, StringSplitOptions.None);
            if (parts.Length < 2) return "?";

            var commandId = parts[1];
            var cmdParts = commandId.Split('_');

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
            if (cmdParts.Length < 2) return commandId;

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

                        if (genericButton != null)
                            return genericButton.CustomLabel ?? actionName;
                    }
                    return cmdParts.Length >= 2 ? cmdParts[1] : "Action";
            }

            return commandId;
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

                var parts = actionParameter.Split(new[] { GetType().FullName }, StringSplitOptions.None);
                if (parts.Length < 2)
                {
                    builder.DrawText("?", BitmapColor.White);
                    return builder.ToImage();
                }

                var commandId = parts[1];
                var cmdParts = commandId.Split('_');
                if (cmdParts.Length < 2)
                {
                    builder.DrawText("?", BitmapColor.White);
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
                                builder.DrawText(device.Name, BitmapColor.White, 11);
                                builder.DrawText(isOn ? "ON" : "OFF", BitmapColor.White, 20);
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
                                builder.DrawText(group.Name, BitmapColor.White, 12);
                                builder.DrawText("ALL", BitmapColor.White, 18);
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
                return;

            var parts = actionParameter.Split(new[] { GetType().FullName }, StringSplitOptions.None);
            if (parts.Length < 2) return;

            var commandId = parts[1];
            var cmdParts = commandId.Split('_');
            if (cmdParts.Length < 2) return;

            _plugin.RecordUserAction();

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
    }
}
