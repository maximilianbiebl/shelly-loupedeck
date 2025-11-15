using System;
using System.Collections.Generic;
using System.Linq;
using Loupedeck;
using ShellyLoupedeckPlugin.Models;

namespace ShellyLoupedeckPlugin.Actions
{
    public class GroupControlFolder : PluginDynamicFolder
    {
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

        public GroupControlFolder()
        {
            DisplayName = "Group Controls";
            Description = "Configurable folder with group control buttons";
        }

        public override bool Load()
        {
            _plugin = (ShellyLoupedeckPlugin)Plugin;
            _plugin.DevicesUpdated += OnDevicesUpdated;
            _plugin.FoldersUpdated += OnFoldersUpdated;

            CreateParameters();
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
            CreateParameters();
        }

        private void OnFoldersUpdated(object sender, EventArgs e)
        {
            CreateParameters();
        }

        private void CreateParameters()
        {
            RemoveAllParameters();

            foreach (var folder in _plugin.Folders)
            {
                AddParameter($"folder_{folder.Id}", folder.Name, "Folders");
            }
        }

        public override IEnumerable<string> GetButtonPressActionNames(string actionParameter)
        {
            var actions = new List<string> { PluginDynamicFolder.NavigateUpActionName };

            if (string.IsNullOrEmpty(actionParameter) || !actionParameter.StartsWith("folder_"))
                return actions;

            var folderId = actionParameter.Substring(7);
            var folder = _plugin.Folders.FirstOrDefault(f => f.Id == folderId);
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

            var folderParam = parts[0];
            var commandId = parts[1];

            if (!folderParam.StartsWith("folder_"))
                return commandId;

            var folderId = folderParam.Substring(7);
            var folder = _plugin.Folders.FirstOrDefault(f => f.Id == folderId);
            if (folder == null) return "N/A";

            var cmdParts = commandId.Split('_');
            if (cmdParts.Length < 2) return commandId;

            var targetId = cmdParts.Length >= 2 ? cmdParts[1] : null;
            var parameter = cmdParts.Length >= 3 ? cmdParts[2] : null;
            var button = folder.Buttons.FirstOrDefault(b => b.TargetId == targetId && b.Parameter == parameter);

            if (button != null && !string.IsNullOrEmpty(button.CustomLabel))
                return button.CustomLabel;

            switch (cmdParts[0])
            {
                case "toggle":
                    var device = _plugin.Devices.FirstOrDefault(d => d.Id == targetId);
                    return device?.Name ?? targetId;

                case "groupcolor":
                    return parameter?.ToUpper() ?? "Color";

                case "groupbrightness":
                    return parameter != null ? $"{parameter}%" : "Brightness";

                case "grouptemp":
                    return parameter != null ? $"{parameter}°C" : "Temperature";

                case "grouptoggle":
                    var group = _plugin.Groups.FirstOrDefault(g => g.Id == targetId);
                    return group?.Name ?? "Toggle";
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
