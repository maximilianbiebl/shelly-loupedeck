using System;
using System.Collections.Generic;
using System.Linq;
using Loupedeck;
using ShellyLoupedeckPlugin.Models;

namespace ShellyLoupedeckPlugin.Actions
{
    public class GroupSwitchFolder : PluginDynamicFolder
    {
        private ShellyLoupedeckPlugin _plugin;

        public GroupSwitchFolder()
        {
            DisplayName = "Group Switches";
            Description = "Switch folder for groups - toggle individual devices";
            GroupName = "Group Folders";
        }

        public override void Load()
        {
            base.Load();

            _plugin = (ShellyLoupedeckPlugin)base.Plugin;
            _plugin.DevicesUpdated += OnDevicesUpdated;
            _plugin.GroupsUpdated += OnGroupsUpdated;

            CreateParameters();
        }

        public override void Unload()
        {
            _plugin.DevicesUpdated -= OnDevicesUpdated;
            _plugin.GroupsUpdated -= OnGroupsUpdated;

            base.Unload();
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

            // Add a folder for each Switch group
            foreach (var group in _plugin.Groups)
            {
                if (group.Purpose == GroupPurpose.Switch)
                {
                    // Create a folder for this group
                    var folderId = $"group_{group.Id}";
                    AddParameter(folderId, $"{group.Name}", group.Name);

                    // Add device switches inside this folder
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

                        var switchParamId = $"{folderId}_{deviceParam}";
                        AddParameter(switchParamId, $"{deviceName}{channelSuffix}", group.Name, folderId);
                    }
                }
            }

            DebugLogger.Log($"GroupSwitchFolder: Created {_plugin.Groups.Count(g => g.Purpose == GroupPurpose.Switch)} folder parameters");
        }

        public override async void RunCommand(string actionParameter)
        {
            DebugLogger.Log($"GroupSwitchFolder: RunCommand called with parameter: {actionParameter}");

            if (string.IsNullOrEmpty(actionParameter))
            {
                DebugLogger.Log("  -> Parameter is null or empty, returning");
                return;
            }

            // Check if this is a folder click (just group_{groupId})
            if (actionParameter.StartsWith("group_") && actionParameter.Split('_').Length == 2)
            {
                DebugLogger.Log("  -> Folder opened, no action needed");
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

            bool currentState = device.GetDeviceState(channel);
            bool newState = !currentState;

            DebugLogger.Log($"    -> Current state: {currentState}, New state: {newState}");

            // Toggle the device based on its type
            var deviceType = device.GetDeviceType();
            bool success = false;

            switch (deviceType)
            {
                case ShellyDeviceType.Switch:
                case ShellyDeviceType.ShellyPlus2PM:
                    success = await _plugin.ApiClient.SetRelayStateAsync(deviceId, channel, newState);
                    break;

                case ShellyDeviceType.ShellyPlusPlugS:
                    success = await _plugin.ApiClient.SetGen3SwitchStateAsync(deviceId, channel, newState);
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

        public override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            // If this is a folder (just group_{groupId}), show a folder icon
            if (actionParameter.StartsWith("group_") && actionParameter.Split('_').Length == 2)
            {
                using (var bitmapBuilder = new BitmapBuilder(imageSize))
                {
                    bitmapBuilder.Clear(new BitmapColor(40, 40, 50));
                    bitmapBuilder.DrawText("📁");
                    return bitmapBuilder.ToImage();
                }
            }

            // For device switches, show ON/OFF state
            if (!actionParameter.StartsWith("group_"))
            {
                return null;
            }

            // Extract device param
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

            bool isOn = device.GetDeviceState(channel);

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
