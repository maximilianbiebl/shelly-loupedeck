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

        // Color presets for Color groups
        private Dictionary<string, (int R, int G, int B, int W)> _colorPresets = new Dictionary<string, (int, int, int, int)>
        {
            { "red", (255, 0, 0, 0) },
            { "green", (0, 255, 0, 0) },
            { "blue", (0, 0, 255, 0) },
            { "white", (0, 0, 0, 255) },
            { "yellow", (255, 255, 0, 0) },
            { "cyan", (0, 255, 255, 0) },
            { "magenta", (255, 0, 255, 0) },
            { "orange", (255, 128, 0, 0) },
            { "purple", (128, 0, 255, 0) }
        };

        // Brightness presets for Brightness groups
        private int[] _brightnessPresets = { 10, 25, 50, 75, 100 };

        // Temperature presets for Thermostat groups (in °C)
        private double[] _temperaturePresets = { 18.0, 19.0, 20.0, 21.0, 22.0, 23.0, 24.0 };

        public GroupControlFolder()
        {
            DisplayName = "Group Controls";
            Description = "Open folders with group-specific controls";
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

            // Create a folder parameter for each group
            foreach (var group in _plugin.Groups)
            {
                AddParameter($"group_{group.Id}", $"{group.Name}", "Groups");
            }
        }

        public override IEnumerable<string> GetButtonPressActionNames(DeviceType deviceType, string actionParameter)
        {
            var actions = new List<string>();

            // Add navigation back button
            actions.Add(PluginDynamicFolder.NavigateUpActionName);

            if (string.IsNullOrEmpty(actionParameter) || !actionParameter.StartsWith("group_"))
            {
                return actions;
            }

            var groupId = actionParameter.Substring(6);
            var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);

            if (group == null)
            {
                return actions;
            }

            // Add buttons based on group purpose
            switch (group.Purpose)
            {
                case GroupPurpose.Switch:
                    // Add a toggle button for each device in the group
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
                                var channelSuffix = channelCount > 1 ? $"_ch{channel}" : "";
                                actions.Add(CreateCommandName($"toggle_{deviceId}{channelSuffix}"));
                            }
                        }
                    }
                    break;

                case GroupPurpose.Color:
                    // Add color preset buttons
                    foreach (var colorKey in _colorPresets.Keys)
                    {
                        actions.Add(CreateCommandName($"color_{colorKey}"));
                    }
                    break;

                case GroupPurpose.Brightness:
                    // Add brightness preset buttons
                    foreach (var brightness in _brightnessPresets)
                    {
                        actions.Add(CreateCommandName($"brightness_{brightness}"));
                    }
                    break;

                case GroupPurpose.Thermostat:
                    // Add temperature preset buttons
                    foreach (var temp in _temperaturePresets)
                    {
                        actions.Add(CreateCommandName($"temp_{temp:F1}"));
                    }
                    break;
            }

            return actions;
        }

        public override string GetCommandDisplayName(string actionParameter, PluginImageSize imageSize)
        {
            if (actionParameter == PluginDynamicFolder.NavigateUpActionName)
            {
                return "Back";
            }

            // Parse command parameter
            var parts = actionParameter.Split(new[] { this.GetType().FullName }, StringSplitOptions.None);
            if (parts.Length < 2)
            {
                return actionParameter;
            }

            var commandId = parts[1];

            if (commandId.StartsWith("toggle_"))
            {
                var deviceParam = commandId.Substring(7);
                var deviceId = deviceParam;
                var channelSuffix = "";

                if (deviceParam.Contains("_ch"))
                {
                    var idx = deviceParam.IndexOf("_ch");
                    deviceId = deviceParam.Substring(0, idx);
                    channelSuffix = deviceParam.Substring(idx);
                }

                var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
                if (device != null)
                {
                    return device.Name + channelSuffix.Replace("_", " ").ToUpper();
                }
                return deviceId;
            }
            else if (commandId.StartsWith("color_"))
            {
                var colorKey = commandId.Substring(6);
                return colorKey.Replace("_", " ").ToUpper();
            }
            else if (commandId.StartsWith("brightness_"))
            {
                var brightness = commandId.Substring(11);
                return $"{brightness}%";
            }
            else if (commandId.StartsWith("temp_"))
            {
                var temp = commandId.Substring(5);
                return $"{temp}°C";
            }

            return commandId;
        }

        public override BitmapImage GetCommandImage(string actionParameter, string actionName, PluginImageSize imageSize)
        {
            using (var builder = new BitmapBuilder(imageSize))
            {
                builder.Clear(BitmapColor.Black);

                if (actionParameter == PluginDynamicFolder.NavigateUpActionName)
                {
                    builder.DrawText("←", BitmapColor.White, imageSize == PluginImageSize.Width90 ? 60 : 40);
                    return builder.ToImage();
                }

                // Parse command parameter
                var parts = actionName.Split(new[] { this.GetType().FullName }, StringSplitOptions.None);
                if (parts.Length < 2)
                {
                    builder.DrawText("?", BitmapColor.White);
                    return builder.ToImage();
                }

                var commandId = parts[1];

                if (commandId.StartsWith("toggle_"))
                {
                    // Get device state and show ON/OFF
                    var deviceParam = commandId.Substring(7);
                    var deviceId = deviceParam;
                    int channel = 0;

                    if (deviceParam.Contains("_ch"))
                    {
                        var idx = deviceParam.IndexOf("_ch");
                        deviceId = deviceParam.Substring(0, idx);
                        int.TryParse(deviceParam.Substring(idx + 3), out channel);
                    }

                    var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
                    if (device != null)
                    {
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

                        var color = isOn ? new BitmapColor(0, 200, 0) : new BitmapColor(100, 100, 100);
                        builder.Clear(color);

                        var deviceName = device.Name;
                        if (deviceParam.Contains("_ch"))
                        {
                            deviceName += deviceParam.Substring(deviceParam.IndexOf("_ch")).Replace("_", " ").ToUpper();
                        }

                        builder.DrawText(deviceName, BitmapColor.White, 12);
                        builder.DrawText(isOn ? "ON" : "OFF", BitmapColor.White, 24);
                    }
                }
                else if (commandId.StartsWith("color_"))
                {
                    var colorKey = commandId.Substring(6);
                    if (_colorPresets.ContainsKey(colorKey))
                    {
                        var color = _colorPresets[colorKey];
                        var bitmapColor = new BitmapColor((byte)color.R, (byte)color.G, (byte)color.B);
                        builder.Clear(bitmapColor);

                        var brightness = (color.R + color.G + color.B) / 3;
                        var textColor = brightness > 128 ? BitmapColor.Black : BitmapColor.White;
                        builder.DrawText(colorKey.Replace("_", " ").ToUpper(), textColor, 14);
                    }
                }
                else if (commandId.StartsWith("brightness_"))
                {
                    var brightnessStr = commandId.Substring(11);
                    if (int.TryParse(brightnessStr, out int brightness))
                    {
                        var grayValue = (byte)(brightness * 2.55);
                        builder.Clear(new BitmapColor(grayValue, grayValue, grayValue));

                        var textColor = brightness > 50 ? BitmapColor.Black : BitmapColor.White;
                        builder.DrawText($"{brightness}%", textColor, 24);
                    }
                }
                else if (commandId.StartsWith("temp_"))
                {
                    var tempStr = commandId.Substring(5);
                    builder.Clear(BitmapColor.Black);
                    builder.DrawText($"{tempStr}°C", BitmapColor.White, 24);
                }

                return builder.ToImage();
            }
        }

        public override void RunCommand(string actionParameter)
        {
            if (actionParameter == PluginDynamicFolder.NavigateUpActionName)
            {
                return; // Navigation is handled by Loupedeck
            }

            // Parse folder parameter and command
            var parts = actionParameter.Split(new[] { this.GetType().FullName }, StringSplitOptions.None);
            if (parts.Length < 2)
            {
                return;
            }

            var folderParam = parts[0];
            var commandId = parts[1];

            if (!folderParam.StartsWith("group_"))
            {
                return;
            }

            var groupId = folderParam.Substring(6);
            var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);

            if (group == null)
            {
                return;
            }

            // Record user action to prevent refresh conflicts
            _plugin.RecordUserAction();

            // Execute command based on type
            if (commandId.StartsWith("toggle_"))
            {
                var deviceParam = commandId.Substring(7);
                var deviceId = deviceParam;
                int channel = 0;

                if (deviceParam.Contains("_ch"))
                {
                    var idx = deviceParam.IndexOf("_ch");
                    deviceId = deviceParam.Substring(0, idx);
                    int.TryParse(deviceParam.Substring(idx + 3), out channel);
                }

                _ = ToggleDeviceAsync(deviceId, channel);
            }
            else if (commandId.StartsWith("color_"))
            {
                var colorKey = commandId.Substring(6);
                if (_colorPresets.ContainsKey(colorKey))
                {
                    var color = _colorPresets[colorKey];
                    _ = SetGroupColorAsync(group, color);
                }
            }
            else if (commandId.StartsWith("brightness_"))
            {
                var brightnessStr = commandId.Substring(11);
                if (int.TryParse(brightnessStr, out int brightness))
                {
                    _ = SetGroupBrightnessAsync(group, brightness);
                }
            }
            else if (commandId.StartsWith("temp_"))
            {
                var tempStr = commandId.Substring(5);
                if (double.TryParse(tempStr, out double temperature))
                {
                    _ = SetGroupTemperatureAsync(group, temperature);
                }
            }
        }

        private async System.Threading.Tasks.Task ToggleDeviceAsync(string deviceId, int channel)
        {
            var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
            if (device == null)
            {
                return;
            }

            // Get current state
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

            // Toggle based on device type
            bool success = false;
            switch (deviceType)
            {
                case ShellyDeviceType.Switch:
                case ShellyDeviceType.ShellyPlus2PM:
                    success = await _plugin.ApiClient.SetRelayStateAsync(deviceId, channel, newState);
                    break;
                case ShellyDeviceType.RGBW:
                case ShellyDeviceType.Dimmer:
                    success = await _plugin.ApiClient.SetLightStateAsync(deviceId, channel, newState);
                    break;
            }

            if (success)
            {
                DebugLogger.Log($"Toggled device {deviceId} channel {channel} to {newState}");
                ActionImageChanged(deviceId);
            }
        }

        private async System.Threading.Tasks.Task SetGroupColorAsync(DeviceGroup group, (int R, int G, int B, int W) color)
        {
            DebugLogger.Log($"Setting color for group {group.Name} to R:{color.R} G:{color.G} B:{color.B} W:{color.W}");

            for (int i = 0; i < group.DeviceIds.Count; i++)
            {
                var deviceId = group.DeviceIds[i];
                var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);

                if (device != null)
                {
                    _plugin.RecordUserAction();

                    var deviceType = device.GetDeviceType();
                    if (deviceType == ShellyDeviceType.RGBW)
                    {
                        // Preserve current brightness
                        int brightness = 100;
                        if (device.Status?.Lights != null && device.Status.Lights.Count > 0)
                        {
                            brightness = device.Status.Lights[0].Brightness;
                        }

                        await _plugin.ApiClient.SetRGBWColorAsync(deviceId, color.R, color.G, color.B, color.W, brightness);
                    }

                    if (i < group.DeviceIds.Count - 1)
                    {
                        await System.Threading.Tasks.Task.Delay(2000);
                    }
                }
            }
        }

        private async System.Threading.Tasks.Task SetGroupBrightnessAsync(DeviceGroup group, int brightness)
        {
            DebugLogger.Log($"Setting brightness for group {group.Name} to {brightness}%");

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

        private async System.Threading.Tasks.Task SetGroupTemperatureAsync(DeviceGroup group, double temperature)
        {
            DebugLogger.Log($"Setting temperature for group {group.Name} to {temperature}°C");

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
