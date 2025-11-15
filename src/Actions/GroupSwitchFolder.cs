using System;
using System.Collections.Generic;
using System.Linq;
using Loupedeck;
using ShellyLoupedeckPlugin.Models;

namespace ShellyLoupedeckPlugin.Actions
{
    public class GroupSwitchFolder : PluginDynamicCommand
    {
        private ShellyLoupedeckPlugin _plugin;

        public GroupSwitchFolder() : base()
        {
            DisplayName = "Group Switches";
            Description = "Switch folder for groups - toggle individual devices";
            GroupName = "Group Folders";
        }

        protected override bool OnLoad()
        {
            _plugin = (ShellyLoupedeckPlugin)base.Plugin;
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

            // Add switches for each Switch group
            foreach (var group in _plugin.Groups)
            {
                if (group.Purpose == GroupPurpose.Switch)
                {
                    // Add device switches for this group
                    foreach (var deviceParam in group.DeviceIds)
                    {
                        // Parse device ID and channel
                        string deviceId = deviceParam;
                        int channel = 0;
                        string channelSuffix = "";

                        if (deviceParam.Contains("_ch"))
                        {
                            var parts = deviceParam.Split(new[] { "_ch" }, StringSplitOptions.None);
                            if (parts.Length == 2)
                            {
                                deviceId = parts[0];
                                int.TryParse(parts[1], out channel);
                                channelSuffix = $" Ch{channel + 1}";
                            }
                        }

                        // Find device name
                        var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
                        var deviceName = device?.Name ?? deviceId;

                        var switchParamId = $"group_{group.Id}_{deviceParam}";
                        AddParameter(switchParamId, $"{group.Name} - {deviceName}{channelSuffix}", group.Name);
                    }
                }
            }

            DebugLogger.Log($"GroupSwitchFolder: Created parameters for {_plugin.Groups.Count(g => g.Purpose == GroupPurpose.Switch)} groups");
        }

        protected override async void RunCommand(string actionParameter)
        {
            DebugLogger.Log($"GroupSwitchFolder: RunCommand called with parameter: {actionParameter}");

            if (string.IsNullOrEmpty(actionParameter))
            {
                DebugLogger.Log("  -> Parameter is null or empty, returning");
                return;
            }

            // Parse parameter: group_{groupId}_{deviceId} or group_{groupId}_{deviceId}_ch{channel}
            if (!actionParameter.StartsWith("group_"))
            {
                DebugLogger.Log("  -> Invalid parameter format");
                return;
            }

            // Extract parts: group_{groupId}_{deviceId}[_ch{N}]
            var withoutPrefix = actionParameter.Substring(6); // Remove "group_"
            var firstUnderscore = withoutPrefix.IndexOf('_');

            if (firstUnderscore == -1)
            {
                DebugLogger.Log("  -> Invalid parameter format (no device part)");
                return;
            }

            var groupId = withoutPrefix.Substring(0, firstUnderscore);
            var deviceParam = withoutPrefix.Substring(firstUnderscore + 1);

            DebugLogger.Log($"  -> Group ID: {groupId}");
            DebugLogger.Log($"  -> Device param: {deviceParam}");

            var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);
            if (group == null)
            {
                DebugLogger.Log($"  -> Group not found!");
                return;
            }

            // Parse device ID and channel
            string deviceId = deviceParam;
            int channel = 0;

            if (deviceParam.Contains("_ch"))
            {
                var parts = deviceParam.Split(new[] { "_ch" }, StringSplitOptions.None);
                if (parts.Length == 2)
                {
                    deviceId = parts[0];
                    int.TryParse(parts[1], out channel);
                }
            }

            DebugLogger.Log($"  -> Device ID: {deviceId}, Channel: {channel}");

            // Record user action
            _plugin.RecordUserAction();

            // Toggle the device
            await ToggleDeviceAsync(deviceId, channel);
        }

        private async System.Threading.Tasks.Task ToggleDeviceAsync(string deviceId, int channel)
        {
            var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
            if (device == null)
            {
                DebugLogger.Log($"    -> Device not found: {deviceId}");
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
            DebugLogger.Log($"    -> Current state: {currentState}, New state: {newState}");

            // Toggle the device based on its type
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

                default:
                    DebugLogger.Log($"    -> Unsupported device type: {deviceType}");
                    return;
            }

            if (success)
            {
                DebugLogger.Log($"    -> Successfully toggled {deviceId} to {newState}");

                // Update device state locally
                await System.Threading.Tasks.Task.Delay(500);
                var updatedDevice = await _plugin.ApiClient.GetDeviceStatusAsync(deviceId);
                if (updatedDevice != null)
                {
                    var index = _plugin.Devices.FindIndex(d => d.Id == deviceId);
                    if (index >= 0)
                    {
                        _plugin.Devices[index] = updatedDevice;
                    }
                }
            }
            else
            {
                DebugLogger.Log($"    -> Failed to toggle {deviceId}");
            }
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            // Extract device param from: group_{groupId}_{deviceParam}
            if (!actionParameter.StartsWith("group_"))
            {
                return null;
            }

            var withoutPrefix = actionParameter.Substring(6); // Remove "group_"
            var firstUnderscore = withoutPrefix.IndexOf('_');

            if (firstUnderscore == -1)
            {
                return null;
            }

            var deviceParam = withoutPrefix.Substring(firstUnderscore + 1);
            string deviceId = deviceParam;
            int channel = 0;

            if (deviceParam.Contains("_ch"))
            {
                var parts = deviceParam.Split(new[] { "_ch" }, StringSplitOptions.None);
                if (parts.Length == 2)
                {
                    deviceId = parts[0];
                    int.TryParse(parts[1], out channel);
                }
            }

            var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
            if (device == null)
            {
                return null;
            }

            // Get device state
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

            using (var bitmapBuilder = new BitmapBuilder(imageSize))
            {
                if (isOn)
                {
                    bitmapBuilder.Clear(new BitmapColor(0, 200, 0)); // Green for ON
                    bitmapBuilder.DrawText("ON", BitmapColor.White);
                }
                else
                {
                    bitmapBuilder.Clear(new BitmapColor(80, 80, 80)); // Gray for OFF
                    bitmapBuilder.DrawText("OFF", BitmapColor.White);
                }

                return bitmapBuilder.ToImage();
            }
        }
    }
}
