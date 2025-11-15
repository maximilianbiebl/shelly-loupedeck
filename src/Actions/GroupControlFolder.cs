using System;
using System.Collections.Generic;
using System.Linq;
using Loupedeck;
using ShellyLoupedeckPlugin.Models;

namespace ShellyLoupedeckPlugin.Actions
{
    /// <summary>
    /// Dynamic folder that provides group-specific controls.
    /// Creates a touchfield folder for each group with buttons based on the group's purpose.
    /// </summary>
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
            { "magenta", (255, 0, 255, 0) }
        };

        // Brightness presets for Brightness groups
        private int[] _brightnessPresets = { 25, 50, 75, 100 };

        public GroupControlFolder()
        {
            DisplayName = "Group Controls";
            Description = "Touchfield folders for group controls";
            GroupName = "Group Actions";
        }

        public override bool Load()
        {
            _plugin = (ShellyLoupedeckPlugin)Plugin;
            _plugin.DevicesUpdated += OnDevicesUpdated;
            _plugin.GroupsUpdated += OnGroupsUpdated;

            CreateParameters();

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

        /// <summary>
        /// Returns all button actions for this folder.
        /// Since we have multiple groups with different purposes, we return a comprehensive set of buttons
        /// that covers all possible actions. The actual visibility/behavior is controlled by the command execution.
        /// </summary>
        public override IEnumerable<string> GetButtonPressActionNames()
        {
            var actions = new List<string>();

            // Navigation button
            actions.Add(PluginDynamicFolder.NavigateUpActionName);

            // Add generic action slots that will be dynamically populated based on the active group
            // We create enough slots for the largest possible group (switches with multiple devices)
            for (int i = 0; i < 15; i++)
            {
                actions.Add(CreateCommandName($"action_{i}"));
            }

            return actions;
        }

        public override string GetCommandDisplayName(string actionParameter, PluginImageSize imageSize)
        {
            if (actionParameter == PluginDynamicFolder.NavigateUpActionName)
            {
                return "Back";
            }

            // Parse action parameter to get group and command info
            var parts = actionParameter.Split(new[] { this.GetType().FullName }, StringSplitOptions.None);
            if (parts.Length < 2)
            {
                return "Action";
            }

            var groupParam = parts[0];
            var commandId = parts[1];

            return commandId.Replace("_", " ");
        }

        public override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var builder = new BitmapBuilder(imageSize))
            {
                builder.Clear(BitmapColor.Black);

                if (actionParameter == PluginDynamicFolder.NavigateUpActionName)
                {
                    builder.DrawText("←", BitmapColor.White, imageSize == PluginImageSize.Width90 ? 60 : 40);
                    return builder.ToImage();
                }

                // Parse action parameter
                var parts = actionParameter.Split(new[] { this.GetType().FullName }, StringSplitOptions.None);
                if (parts.Length < 2)
                {
                    builder.DrawText("?", BitmapColor.White);
                    return builder.ToImage();
                }

                var groupParam = parts[0];
                var commandId = parts[1];

                // Extract group ID
                if (!groupParam.StartsWith("group_"))
                {
                    builder.DrawText("ERR", BitmapColor.White);
                    return builder.ToImage();
                }

                var groupId = groupParam.Substring(6);
                var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);

                if (group == null)
                {
                    builder.DrawText("N/A", BitmapColor.White);
                    return builder.ToImage();
                }

                // Parse command index
                if (commandId.StartsWith("action_"))
                {
                    var indexStr = commandId.Substring(7);
                    if (int.TryParse(indexStr, out int actionIndex))
                    {
                        return RenderActionButton(builder, group, actionIndex, imageSize);
                    }
                }

                builder.DrawText(commandId, BitmapColor.White, 10);
                return builder.ToImage();
            }
        }

        private BitmapImage RenderActionButton(BitmapBuilder builder, DeviceGroup group, int actionIndex, PluginImageSize imageSize)
        {
            switch (group.Purpose)
            {
                case GroupPurpose.Switch:
                    return RenderSwitchAction(builder, group, actionIndex);

                case GroupPurpose.Color:
                    return RenderColorAction(builder, group, actionIndex);

                case GroupPurpose.Brightness:
                    return RenderBrightnessAction(builder, group, actionIndex);

                case GroupPurpose.Thermostat:
                    return RenderThermostatAction(builder, group, actionIndex);

                default:
                    builder.Clear(BitmapColor.Black);
                    builder.DrawText("N/A", BitmapColor.White);
                    return builder.ToImage();
            }
        }

        private BitmapImage RenderSwitchAction(BitmapBuilder builder, DeviceGroup group, int actionIndex)
        {
            // Count total channels across all devices
            var deviceChannels = new List<(string DeviceId, int Channel, string DeviceName)>();

            foreach (var deviceId in group.DeviceIds)
            {
                var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
                if (device != null)
                {
                    var deviceType = device.GetDeviceType();
                    int channelCount = 1;

                    if (deviceType == ShellyDeviceType.ShellyPlus2PM)
                    {
                        channelCount = device.Status?.Relays?.Count ?? 1;
                    }
                    else if (deviceType == ShellyDeviceType.RGBW || deviceType == ShellyDeviceType.Dimmer)
                    {
                        channelCount = device.Status?.Lights?.Count ?? 1;
                    }

                    for (int ch = 0; ch < channelCount; ch++)
                    {
                        var suffix = channelCount > 1 ? $" CH{ch}" : "";
                        deviceChannels.Add((deviceId, ch, device.Name + suffix));
                    }
                }
            }

            if (actionIndex >= deviceChannels.Count)
            {
                builder.Clear(new BitmapColor(20, 20, 20));
                return builder.ToImage();
            }

            var (devId, channel, devName) = deviceChannels[actionIndex];
            var dev = _plugin.Devices.FirstOrDefault(d => d.Id == devId);

            if (dev != null)
            {
                bool isOn = false;
                var devType = dev.GetDeviceType();

                if (devType == ShellyDeviceType.RGBW || devType == ShellyDeviceType.Dimmer)
                {
                    if (dev.Status?.Lights != null && dev.Status.Lights.Count > channel)
                    {
                        isOn = dev.Status.Lights[channel].IsOn;
                    }
                }
                else
                {
                    if (dev.Status?.Relays != null && dev.Status.Relays.Count > channel)
                    {
                        isOn = dev.Status.Relays[channel].IsOn;
                    }
                }

                var color = isOn ? new BitmapColor(0, 200, 0) : new BitmapColor(100, 100, 100);
                builder.Clear(color);
                builder.DrawText(devName, BitmapColor.White, 11);
                builder.DrawText(isOn ? "ON" : "OFF", BitmapColor.White, 20);
            }
            else
            {
                builder.Clear(BitmapColor.Black);
                builder.DrawText("ERR", BitmapColor.White);
            }

            return builder.ToImage();
        }

        private BitmapImage RenderColorAction(BitmapBuilder builder, DeviceGroup group, int actionIndex)
        {
            var colorKeys = _colorPresets.Keys.ToList();

            if (actionIndex >= colorKeys.Count)
            {
                builder.Clear(new BitmapColor(20, 20, 20));
                return builder.ToImage();
            }

            var colorKey = colorKeys[actionIndex];
            var color = _colorPresets[colorKey];

            var bitmapColor = new BitmapColor((byte)color.R, (byte)color.G, (byte)color.B);
            builder.Clear(bitmapColor);

            var brightness = (color.R + color.G + color.B) / 3;
            var textColor = brightness > 128 ? BitmapColor.Black : BitmapColor.White;
            builder.DrawText(colorKey.ToUpper(), textColor, 16);

            return builder.ToImage();
        }

        private BitmapImage RenderBrightnessAction(BitmapBuilder builder, DeviceGroup group, int actionIndex)
        {
            if (actionIndex >= _brightnessPresets.Length)
            {
                builder.Clear(new BitmapColor(20, 20, 20));
                return builder.ToImage();
            }

            var brightness = _brightnessPresets[actionIndex];
            var grayValue = (byte)(brightness * 2.55);
            builder.Clear(new BitmapColor(grayValue, grayValue, grayValue));

            var textColor = brightness > 50 ? BitmapColor.Black : BitmapColor.White;
            builder.DrawText($"{brightness}%", textColor, 24);

            return builder.ToImage();
        }

        private BitmapImage RenderThermostatAction(BitmapBuilder builder, DeviceGroup group, int actionIndex)
        {
            var temperatures = new double[] { 18.0, 19.0, 20.0, 21.0, 22.0, 23.0, 24.0 };

            if (actionIndex >= temperatures.Length)
            {
                builder.Clear(new BitmapColor(20, 20, 20));
                return builder.ToImage();
            }

            var temp = temperatures[actionIndex];
            builder.Clear(BitmapColor.Black);
            builder.DrawText($"{temp:F1}°C", BitmapColor.White, 22);

            return builder.ToImage();
        }

        public override void RunCommand(string actionParameter)
        {
            if (actionParameter == PluginDynamicFolder.NavigateUpActionName)
            {
                return; // Handled by Loupedeck
            }

            // Parse action parameter
            var parts = actionParameter.Split(new[] { this.GetType().FullName }, StringSplitOptions.None);
            if (parts.Length < 2)
            {
                return;
            }

            var groupParam = parts[0];
            var commandId = parts[1];

            if (!groupParam.StartsWith("group_"))
            {
                return;
            }

            var groupId = groupParam.Substring(6);
            var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);

            if (group == null)
            {
                return;
            }

            // Parse command index
            if (!commandId.StartsWith("action_"))
            {
                return;
            }

            var indexStr = commandId.Substring(7);
            if (!int.TryParse(indexStr, out int actionIndex))
            {
                return;
            }

            // Record user action
            _plugin.RecordUserAction();

            // Execute based on group purpose
            switch (group.Purpose)
            {
                case GroupPurpose.Switch:
                    ExecuteSwitchAction(group, actionIndex);
                    break;

                case GroupPurpose.Color:
                    ExecuteColorAction(group, actionIndex);
                    break;

                case GroupPurpose.Brightness:
                    ExecuteBrightnessAction(group, actionIndex);
                    break;

                case GroupPurpose.Thermostat:
                    ExecuteThermostatAction(group, actionIndex);
                    break;
            }
        }

        private async void ExecuteSwitchAction(DeviceGroup group, int actionIndex)
        {
            var deviceChannels = new List<(string DeviceId, int Channel)>();

            foreach (var deviceId in group.DeviceIds)
            {
                var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
                if (device != null)
                {
                    var deviceType = device.GetDeviceType();
                    int channelCount = 1;

                    if (deviceType == ShellyDeviceType.ShellyPlus2PM)
                    {
                        channelCount = device.Status?.Relays?.Count ?? 1;
                    }
                    else if (deviceType == ShellyDeviceType.RGBW || deviceType == ShellyDeviceType.Dimmer)
                    {
                        channelCount = device.Status?.Lights?.Count ?? 1;
                    }

                    for (int ch = 0; ch < channelCount; ch++)
                    {
                        deviceChannels.Add((deviceId, ch));
                    }
                }
            }

            if (actionIndex >= deviceChannels.Count)
            {
                return;
            }

            var (devId, channel) = deviceChannels[actionIndex];
            var dev = _plugin.Devices.FirstOrDefault(d => d.Id == devId);

            if (dev != null)
            {
                bool currentState = false;
                var devType = dev.GetDeviceType();

                if (devType == ShellyDeviceType.RGBW || devType == ShellyDeviceType.Dimmer)
                {
                    if (dev.Status?.Lights != null && dev.Status.Lights.Count > channel)
                    {
                        currentState = dev.Status.Lights[channel].IsOn;
                    }
                }
                else
                {
                    if (dev.Status?.Relays != null && dev.Status.Relays.Count > channel)
                    {
                        currentState = dev.Status.Relays[channel].IsOn;
                    }
                }

                bool newState = !currentState;

                switch (devType)
                {
                    case ShellyDeviceType.Switch:
                    case ShellyDeviceType.ShellyPlus2PM:
                        await _plugin.ApiClient.SetRelayStateAsync(devId, channel, newState);
                        break;
                    case ShellyDeviceType.RGBW:
                    case ShellyDeviceType.Dimmer:
                        await _plugin.ApiClient.SetLightStateAsync(devId, channel, newState);
                        break;
                }

                ActionImageChanged();
            }
        }

        private async void ExecuteColorAction(DeviceGroup group, int actionIndex)
        {
            var colorKeys = _colorPresets.Keys.ToList();

            if (actionIndex >= colorKeys.Count)
            {
                return;
            }

            var colorKey = colorKeys[actionIndex];
            var color = _colorPresets[colorKey];

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

        private async void ExecuteBrightnessAction(DeviceGroup group, int actionIndex)
        {
            if (actionIndex >= _brightnessPresets.Length)
            {
                return;
            }

            var brightness = _brightnessPresets[actionIndex];

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

        private async void ExecuteThermostatAction(DeviceGroup group, int actionIndex)
        {
            var temperatures = new double[] { 18.0, 19.0, 20.0, 21.0, 22.0, 23.0, 24.0 };

            if (actionIndex >= temperatures.Length)
            {
                return;
            }

            var temperature = temperatures[actionIndex];

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
