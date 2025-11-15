using System;
using System.Collections.Generic;
using System.Linq;
using Loupedeck;
using ShellyLoupedeckPlugin.Models;

namespace ShellyLoupedeckPlugin.Actions
{
    /// <summary>
    /// Provides quick-access control buttons for groups.
    /// Each group gets multiple buttons based on its purpose (colors, brightness levels, device toggles, etc.)
    /// Buttons are grouped by the group name in the UI.
    /// </summary>
    public class GroupControlFolder : PluginDynamicCommand
    {
        private ShellyLoupedeckPlugin _plugin;

        // Color presets for Color groups
        private Dictionary<string, (int R, int G, int B, int W)> _colorPresets = new Dictionary<string, (int, int, int, int)>
        {
            { "Red", (255, 0, 0, 0) },
            { "Green", (0, 255, 0, 0) },
            { "Blue", (0, 0, 255, 0) },
            { "White", (0, 0, 0, 255) },
            { "Yellow", (255, 255, 0, 0) },
            { "Cyan", (0, 255, 255, 0) },
            { "Magenta", (255, 0, 255, 0) },
            { "Orange", (255, 128, 0, 0) }
        };

        // Brightness presets for Brightness groups
        private Dictionary<string, int> _brightnessPresets = new Dictionary<string, int>
        {
            { "25%", 25 },
            { "50%", 50 },
            { "75%", 75 },
            { "100%", 100 }
        };

        // Temperature presets for Thermostat groups
        private Dictionary<string, double> _temperaturePresets = new Dictionary<string, double>
        {
            { "18°C", 18.0 },
            { "19°C", 19.0 },
            { "20°C", 20.0 },
            { "21°C", 21.0 },
            { "22°C", 22.0 },
            { "23°C", 23.0 },
            { "24°C", 24.0 }
        };

        public GroupControlFolder()
        {
            DisplayName = "Group Quick Controls";
            Description = "Quick access buttons for group controls";
            GroupName = "Group Actions";
        }

        protected override bool OnLoad()
        {
            _plugin = (ShellyLoupedeckPlugin)Plugin;
            _plugin.DevicesUpdated += OnDevicesUpdated;
            _plugin.GroupsUpdated += OnGroupsUpdated;

            CreateParameters();

            return base.OnLoad();
        }

        protected override bool OnUnload()
        {
            _plugin.DevicesUpdated -= OnDevicesUpdated;
            _plugin.GroupsUpdated -= OnGroupsUpdated;

            return base.OnUnload();
        }

        private void OnDevicesUpdated(object sender, EventArgs e)
        {
            CreateParameters();
        }

        private void OnGroupsUpdated(object sender, EventArgs e)
        {
            CreateParameters();
        }

        private void CreateParameters()
        {
            RemoveAllParameters();

            // Create buttons for each group based on its purpose
            foreach (var group in _plugin.Groups)
            {
                var groupDisplayName = $"[{group.Name}]";

                switch (group.Purpose)
                {
                    case GroupPurpose.Switch:
                        CreateSwitchParameters(group, groupDisplayName);
                        break;

                    case GroupPurpose.Color:
                        CreateColorParameters(group, groupDisplayName);
                        break;

                    case GroupPurpose.Brightness:
                        CreateBrightnessParameters(group, groupDisplayName);
                        break;

                    case GroupPurpose.Thermostat:
                        CreateThermostatParameters(group, groupDisplayName);
                        break;
                }
            }

            ActionImageChanged();
        }

        private void CreateSwitchParameters(DeviceGroup group, string groupDisplayName)
        {
            // Add toggle button for each device in the group
            foreach (var deviceId in group.DeviceIds)
            {
                var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
                if (device != null)
                {
                    var deviceType = device.GetDeviceType();
                    int channelCount = 1;

                    // Determine channel count
                    if (deviceType == ShellyDeviceType.ShellyPlus2PM)
                    {
                        channelCount = device.Status?.Relays?.Count ?? 1;
                    }
                    else if (deviceType == ShellyDeviceType.RGBW || deviceType == ShellyDeviceType.Dimmer)
                    {
                        channelCount = device.Status?.Lights?.Count ?? 1;
                    }

                    // Add button for each channel
                    for (int channel = 0; channel < channelCount; channel++)
                    {
                        var channelSuffix = channelCount > 1 ? $" CH{channel}" : "";
                        var paramId = $"group_{group.Id}_toggle_{deviceId}_ch{channel}";
                        var displayName = $"{device.Name}{channelSuffix}";
                        AddParameter(paramId, displayName, groupDisplayName);
                    }
                }
            }
        }

        private void CreateColorParameters(DeviceGroup group, string groupDisplayName)
        {
            // Add color preset buttons
            foreach (var color in _colorPresets)
            {
                var paramId = $"group_{group.Id}_color_{color.Key.ToLower()}";
                AddParameter(paramId, color.Key, groupDisplayName);
            }
        }

        private void CreateBrightnessParameters(DeviceGroup group, string groupDisplayName)
        {
            // Add brightness preset buttons
            foreach (var preset in _brightnessPresets)
            {
                var paramId = $"group_{group.Id}_brightness_{preset.Value}";
                AddParameter(paramId, preset.Key, groupDisplayName);
            }
        }

        private void CreateThermostatParameters(DeviceGroup group, string groupDisplayName)
        {
            // Add temperature preset buttons
            foreach (var temp in _temperaturePresets)
            {
                var paramId = $"group_{group.Id}_temp_{temp.Value:F1}";
                AddParameter(paramId, temp.Key, groupDisplayName);
            }
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var builder = new BitmapBuilder(imageSize))
            {
                builder.Clear(BitmapColor.Black);

                if (string.IsNullOrEmpty(actionParameter))
                {
                    builder.DrawText("Group", BitmapColor.White);
                    return builder.ToImage();
                }

                // Parse parameter: group_{groupId}_{action}_{details}
                var parts = actionParameter.Split('_');
                if (parts.Length < 3)
                {
                    builder.DrawText("?", BitmapColor.White);
                    return builder.ToImage();
                }

                var groupId = parts[1];
                var action = parts[2];

                var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);
                if (group == null)
                {
                    builder.DrawText("N/A", BitmapColor.White);
                    return builder.ToImage();
                }

                switch (action)
                {
                    case "toggle":
                        return RenderToggleButton(builder, parts, imageSize);

                    case "color":
                        return RenderColorButton(builder, parts, imageSize);

                    case "brightness":
                        return RenderBrightnessButton(builder, parts, imageSize);

                    case "temp":
                        return RenderThermostatButton(builder, parts, imageSize);

                    default:
                        builder.DrawText(action, BitmapColor.White);
                        return builder.ToImage();
                }
            }
        }

        private BitmapImage RenderToggleButton(BitmapBuilder builder, string[] parts, PluginImageSize imageSize)
        {
            // Format: group_{groupId}_toggle_{deviceId}_ch{channel}
            if (parts.Length < 6)
            {
                builder.DrawText("?", BitmapColor.White);
                return builder.ToImage();
            }

            var deviceId = parts[3];
            int.TryParse(parts[5], out int channel);

            var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
            if (device == null)
            {
                builder.DrawText("N/A", BitmapColor.White);
                return builder.ToImage();
            }

            bool isOn = false;
            var deviceType = device.GetDeviceType();

            if (deviceType == ShellyDeviceType.RGBW || deviceType == ShellyDeviceType.Dimmer)
            {
                if (device.Status?.Lights != null && device.Status.Lights.Count > channel)
                {
                    isOn = device.Status.Lights[channel].IsOn;
                }
            }
            else
            {
                if (device.Status?.Relays != null && device.Status.Relays.Count > channel)
                {
                    isOn = device.Status.Relays[channel].IsOn;
                }
            }

            var color = isOn ? new BitmapColor(0, 200, 0) : new BitmapColor(80, 80, 80);
            builder.Clear(color);

            var deviceName = device.Name;
            if (parts.Length > 5 && parts[4] == "ch" && int.Parse(parts[5]) > 0)
            {
                deviceName += $" CH{parts[5]}";
            }

            builder.DrawText(deviceName, BitmapColor.White, 11);
            builder.DrawText(isOn ? "ON" : "OFF", BitmapColor.White, 22);

            return builder.ToImage();
        }

        private BitmapImage RenderColorButton(BitmapBuilder builder, string[] parts, PluginImageSize imageSize)
        {
            // Format: group_{groupId}_color_{colorname}
            if (parts.Length < 4)
            {
                builder.DrawText("?", BitmapColor.White);
                return builder.ToImage();
            }

            var colorKey = parts[3];
            var colorName = colorKey.Substring(0, 1).ToUpper() + colorKey.Substring(1);

            if (!_colorPresets.ContainsKey(colorName))
            {
                builder.DrawText(colorName, BitmapColor.White);
                return builder.ToImage();
            }

            var color = _colorPresets[colorName];
            var bitmapColor = new BitmapColor((byte)color.R, (byte)color.G, (byte)color.B);
            builder.Clear(bitmapColor);

            var brightness = (color.R + color.G + color.B) / 3;
            var textColor = brightness > 128 ? BitmapColor.Black : BitmapColor.White;
            builder.DrawText(colorName, textColor, 16);

            return builder.ToImage();
        }

        private BitmapImage RenderBrightnessButton(BitmapBuilder builder, string[] parts, PluginImageSize imageSize)
        {
            // Format: group_{groupId}_brightness_{value}
            if (parts.Length < 4)
            {
                builder.DrawText("?", BitmapColor.White);
                return builder.ToImage();
            }

            if (!int.TryParse(parts[3], out int brightness))
            {
                builder.DrawText("?", BitmapColor.White);
                return builder.ToImage();
            }

            var grayValue = (byte)(brightness * 2.55);
            builder.Clear(new BitmapColor(grayValue, grayValue, grayValue));

            var textColor = brightness > 50 ? BitmapColor.Black : BitmapColor.White;
            builder.DrawText($"{brightness}%", textColor, 24);

            return builder.ToImage();
        }

        private BitmapImage RenderThermostatButton(BitmapBuilder builder, string[] parts, PluginImageSize imageSize)
        {
            // Format: group_{groupId}_temp_{value}
            if (parts.Length < 4)
            {
                builder.DrawText("?", BitmapColor.White);
                return builder.ToImage();
            }

            var tempStr = parts[3];
            builder.Clear(BitmapColor.Black);
            builder.DrawText($"{tempStr}°C", BitmapColor.White, 22);

            return builder.ToImage();
        }

        protected override async void RunCommand(string actionParameter)
        {
            if (string.IsNullOrEmpty(actionParameter))
            {
                return;
            }

            // Parse parameter
            var parts = actionParameter.Split('_');
            if (parts.Length < 3)
            {
                return;
            }

            var groupId = parts[1];
            var action = parts[2];

            var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);
            if (group == null)
            {
                return;
            }

            // Record user action
            _plugin.RecordUserAction();

            switch (action)
            {
                case "toggle":
                    await ExecuteToggleAction(parts);
                    break;

                case "color":
                    await ExecuteColorAction(group, parts);
                    break;

                case "brightness":
                    await ExecuteBrightnessAction(group, parts);
                    break;

                case "temp":
                    await ExecuteThermostatAction(group, parts);
                    break;
            }
        }

        private async System.Threading.Tasks.Task ExecuteToggleAction(string[] parts)
        {
            // Format: group_{groupId}_toggle_{deviceId}_ch{channel}
            if (parts.Length < 6)
            {
                return;
            }

            var deviceId = parts[3];
            int.TryParse(parts[5], out int channel);

            var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
            if (device == null)
            {
                return;
            }

            bool currentState = false;
            var deviceType = device.GetDeviceType();

            if (deviceType == ShellyDeviceType.RGBW || deviceType == ShellyDeviceType.Dimmer)
            {
                if (device.Status?.Lights != null && device.Status.Lights.Count > channel)
                {
                    currentState = device.Status.Lights[channel].IsOn;
                }
            }
            else
            {
                if (device.Status?.Relays != null && device.Status.Relays.Count > channel)
                {
                    currentState = device.Status.Relays[channel].IsOn;
                }
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

            ActionImageChanged();
        }

        private async System.Threading.Tasks.Task ExecuteColorAction(DeviceGroup group, string[] parts)
        {
            // Format: group_{groupId}_color_{colorname}
            if (parts.Length < 4)
            {
                return;
            }

            var colorKey = parts[3];
            var colorName = colorKey.Substring(0, 1).ToUpper() + colorKey.Substring(1);

            if (!_colorPresets.ContainsKey(colorName))
            {
                return;
            }

            var color = _colorPresets[colorName];

            for (int i = 0; i < group.DeviceIds.Count; i++)
            {
                var deviceId = group.DeviceIds[i];
                var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);

                if (device != null && device.GetDeviceType() == ShellyDeviceType.RGBW)
                {
                    _plugin.RecordUserAction();

                    int brightness = 100;
                    if (device.Status?.Lights != null && device.Status.Lights.Count > 0)
                    {
                        brightness = device.Status.Lights[0].Brightness;
                    }

                    await _plugin.ApiClient.SetLightColorAsync(deviceId, color.R, color.G, color.B, color.W, brightness: brightness);

                    if (i < group.DeviceIds.Count - 1)
                    {
                        await System.Threading.Tasks.Task.Delay(2000);
                    }
                }
            }
        }

        private async System.Threading.Tasks.Task ExecuteBrightnessAction(DeviceGroup group, string[] parts)
        {
            // Format: group_{groupId}_brightness_{value}
            if (parts.Length < 4)
            {
                return;
            }

            if (!int.TryParse(parts[3], out int brightness))
            {
                return;
            }

            for (int i = 0; i < group.DeviceIds.Count; i++)
            {
                var deviceId = group.DeviceIds[i];
                _plugin.RecordUserAction();

                await _plugin.ApiClient.SetLightBrightnessAsync(deviceId, brightness);

                if (i < group.DeviceIds.Count - 1)
                {
                    await System.Threading.Tasks.Task.Delay(2000);
                }
            }
        }

        private async System.Threading.Tasks.Task ExecuteThermostatAction(DeviceGroup group, string[] parts)
        {
            // Format: group_{groupId}_temp_{value}
            if (parts.Length < 4)
            {
                return;
            }

            if (!double.TryParse(parts[3], out double temperature))
            {
                return;
            }

            for (int i = 0; i < group.DeviceIds.Count; i++)
            {
                var deviceId = group.DeviceIds[i];
                _plugin.RecordUserAction();

                await _plugin.ApiClient.SetThermostatTemperatureAsync(deviceId, temperature);

                if (i < group.DeviceIds.Count - 1)
                {
                    await System.Threading.Tasks.Task.Delay(2000);
                }
            }
        }
    }
}
