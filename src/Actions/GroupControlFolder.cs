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
        private List<string> _buttonActions = new List<string>();

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
            Description = "Folder with group control buttons";
        }

        public override bool Load()
        {
            _plugin = (ShellyLoupedeckPlugin)Plugin;
            _plugin.DevicesUpdated += OnDevicesUpdated;
            _plugin.GroupsUpdated += OnGroupsUpdated;

            BuildButtonList();
            return true;
        }

        public override bool Unload()
        {
            _plugin.DevicesUpdated -= OnDevicesUpdated;
            _plugin.GroupsUpdated -= OnGroupsUpdated;
            return true;
        }

        private void OnDevicesUpdated(object sender, EventArgs e)
        {
            BuildButtonList();
        }

        private void OnGroupsUpdated(object sender, EventArgs e)
        {
            BuildButtonList();
        }

        private void BuildButtonList()
        {
            _buttonActions.Clear();

            foreach (var group in _plugin.Groups)
            {
                switch (group.Purpose)
                {
                    case GroupPurpose.Switch:
                        foreach (var deviceId in group.DeviceIds)
                        {
                            var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
                            if (device != null)
                            {
                                var deviceType = device.GetDeviceType();
                                int channelCount = 1;

                                if (deviceType == ShellyDeviceType.ShellyPlus2PM)
                                    channelCount = device.Status?.Relays?.Count ?? 1;
                                else if (deviceType == ShellyDeviceType.RGBW || deviceType == ShellyDeviceType.Dimmer)
                                    channelCount = device.Status?.Lights?.Count ?? 1;

                                for (int ch = 0; ch < channelCount; ch++)
                                    _buttonActions.Add($"toggle_{group.Id}_{deviceId}_ch{ch}");
                            }
                        }
                        break;

                    case GroupPurpose.Color:
                        foreach (var color in _colorPresets.Keys)
                            _buttonActions.Add($"color_{group.Id}_{color}");
                        break;

                    case GroupPurpose.Brightness:
                        foreach (var brightness in new[] { 25, 50, 75, 100 })
                            _buttonActions.Add($"brightness_{group.Id}_{brightness}");
                        break;

                    case GroupPurpose.Thermostat:
                        foreach (var temp in new[] { 18.0, 19.0, 20.0, 21.0, 22.0, 23.0, 24.0 })
                            _buttonActions.Add($"temp_{group.Id}_{temp:F1}");
                        break;
                }
            }
        }

        public override IEnumerable<string> GetButtonPressActionNames()
        {
            var actions = new List<string> { PluginDynamicFolder.NavigateUpActionName };

            foreach (var action in _buttonActions)
                actions.Add(CreateCommandName(action));

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
            if (cmdParts.Length < 3) return commandId;

            var groupId = cmdParts[1];
            var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);
            if (group == null) return "N/A";

            switch (cmdParts[0])
            {
                case "toggle":
                    var deviceId = cmdParts[2];
                    var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
                    return device?.Name ?? deviceId;

                case "color":
                    return $"{group.Name} - {cmdParts[2].ToUpper()}";

                case "brightness":
                    return $"{group.Name} - {cmdParts[2]}%";

                case "temp":
                    return $"{group.Name} - {cmdParts[2]}°C";
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
                if (cmdParts.Length < 3)
                {
                    builder.DrawText("?", BitmapColor.White);
                    return builder.ToImage();
                }

                var groupId = cmdParts[1];
                var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);

                switch (cmdParts[0])
                {
                    case "toggle":
                        if (cmdParts.Length >= 5)
                        {
                            var deviceId = cmdParts[2];
                            int.TryParse(cmdParts[4], out int channel);
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

                    case "color":
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

                    case "brightness":
                        if (cmdParts.Length >= 3 && int.TryParse(cmdParts[2], out int bright))
                        {
                            var grayValue = (byte)(bright * 2.55);
                            builder.Clear(new BitmapColor(grayValue, grayValue, grayValue));
                            builder.DrawText($"{bright}%", bright > 50 ? BitmapColor.Black : BitmapColor.White, 22);
                        }
                        break;

                    case "temp":
                        if (cmdParts.Length >= 3)
                        {
                            builder.Clear(BitmapColor.Black);
                            builder.DrawText($"{cmdParts[2]}°C", BitmapColor.White, 20);
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
            if (cmdParts.Length < 3) return;

            var groupId = cmdParts[1];
            var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);
            if (group == null) return;

            _plugin.RecordUserAction();

            switch (cmdParts[0])
            {
                case "toggle":
                    if (cmdParts.Length >= 5)
                    {
                        var deviceId = cmdParts[2];
                        int.TryParse(cmdParts[4], out int channel);
                        _ = ToggleDeviceAsync(deviceId, channel);
                    }
                    break;

                case "color":
                    if (cmdParts.Length >= 3)
                    {
                        var colorKey = cmdParts[2];
                        if (_colorPresets.ContainsKey(colorKey))
                            _ = SetGroupColorAsync(group, _colorPresets[colorKey]);
                    }
                    break;

                case "brightness":
                    if (cmdParts.Length >= 3 && int.TryParse(cmdParts[2], out int brightness))
                        _ = SetGroupBrightnessAsync(group, brightness);
                    break;

                case "temp":
                    if (cmdParts.Length >= 3 && double.TryParse(cmdParts[2], out double temp))
                        _ = SetGroupTemperatureAsync(group, temp);
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
