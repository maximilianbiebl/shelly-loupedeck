using System;
using System.Linq;
using System.Threading.Tasks;
using Loupedeck;
using ShellyLoupedeckPlugin.Models;

namespace ShellyLoupedeckPlugin.Actions
{
    public class DeviceSwitchAction : PluginDynamicCommand
    {
        private ShellyLoupedeckPlugin _plugin;

        public DeviceSwitchAction() : base()
        {
            DisplayName = "Device Switch";
            Description = "Toggle Shelly devices on/off";
            GroupName = "Controls";
        }

        protected override bool OnLoad()
        {
            _plugin = (ShellyLoupedeckPlugin)Plugin;
            _plugin.DevicesUpdated += OnDevicesUpdated;

            // Initial parameter creation
            CreateParameters();

            return base.OnLoad();
        }

        protected override bool OnUnload()
        {
            _plugin.DevicesUpdated -= OnDevicesUpdated;
            return base.OnUnload();
        }

        private void OnDevicesUpdated(object sender, EventArgs e)
        {
            DebugLogger.Log("=== DeviceSwitchAction: OnDevicesUpdated called ===");
            CreateParameters();
        }

        private void CreateParameters()
        {
            DebugLogger.Log($"=== DeviceSwitchAction: CreateParameters called, Plugin has {_plugin.Devices.Count} devices ===");

            // Remove all existing parameters
            RemoveAllParameters();

            // Add individual devices
            int deviceCount = 0;
            foreach (var device in _plugin.Devices)
            {
                var deviceType = device.GetDeviceType();
                DebugLogger.Log($"  Device {device.Id} ({device.Name}): Type={deviceType}");

                if (deviceType == ShellyDeviceType.Switch ||
                    deviceType == ShellyDeviceType.ShellyPlus2PM ||
                    deviceType == ShellyDeviceType.RGBW ||
                    deviceType == ShellyDeviceType.Dimmer)
                {
                    // Check if device has multiple relays (like Shelly Switch 2.5)
                    int relayCount = 0;
                    if (device.Status?.Relays != null)
                    {
                        relayCount = device.Status.Relays.Count;
                    }
                    else if (device.Relays != null)
                    {
                        relayCount = device.Relays.Count;
                    }

                    if (relayCount > 1)
                    {
                        // Add parameter for each relay
                        for (int i = 0; i < relayCount; i++)
                        {
                            AddParameter($"{device.Id}_ch{i}", $"{device.Name} - Channel {i + 1}", "Devices");
                            DebugLogger.Log($"    -> Added as parameter with channel {i}");
                            deviceCount++;
                        }
                    }
                    else
                    {
                        // Single relay/light device
                        AddParameter(device.Id, device.Name, "Devices");
                        DebugLogger.Log($"    -> Added as parameter");
                        deviceCount++;
                    }
                }
                else
                {
                    DebugLogger.Log($"    -> Skipped (wrong type for switch action)");
                }
            }

            DebugLogger.Log($"DeviceSwitchAction: Added {deviceCount} device parameters");

            // Add groups
            foreach (var group in _plugin.Groups)
            {
                if (group.Purpose == GroupPurpose.Switch)
                {
                    AddParameter($"group_{group.Id}", $"[Group] {group.Name}", "Groups");
                    deviceCount++;
                }
            }

            // If no devices, add info parameter
            if (deviceCount == 0)
            {
                DebugLogger.Log("DeviceSwitchAction: No devices found, adding info parameter");
                AddParameter("__no_devices", "No devices configured", "Info");
            }

            ActionImageChanged();
        }

        protected override async void RunCommand(string actionParameter)
        {
            DebugLogger.Log($"=== DeviceSwitchAction: RunCommand called with parameter: {actionParameter} ===");

            if (string.IsNullOrEmpty(actionParameter))
            {
                DebugLogger.Log("  -> Parameter is null or empty, returning");
                return;
            }

            if (actionParameter.StartsWith("group_"))
            {
                var groupId = actionParameter.Substring(6);
                DebugLogger.Log($"  -> Group action for group ID: {groupId}");
                var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);
                if (group != null)
                {
                    DebugLogger.Log($"  -> Found group with {group.DeviceIds.Count} devices, calling sequentially to avoid rate limit");
                    for (int i = 0; i < group.DeviceIds.Count; i++)
                    {
                        var deviceParam = group.DeviceIds[i];

                        // Parse device ID and channel (format: deviceId or deviceId_chN)
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

                        DebugLogger.Log($"  -> Group device {i+1}/{group.DeviceIds.Count}: {deviceId}, channel: {channel}");
                        await ToggleDeviceAsync(deviceId, channel, skipStatusRefresh: true);

                        // Add 1.5 second delay between devices to respect rate limit (except after last device)
                        if (i < group.DeviceIds.Count - 1)
                        {
                            DebugLogger.Log($"  -> Waiting 1500ms before next device (rate limit prevention)");
                            await Task.Delay(1500);
                        }
                    }
                    DebugLogger.Log($"  -> Group operation complete, status will be refreshed by background task");
                }
                else
                {
                    DebugLogger.Log($"  -> Group not found!");
                }
            }
            else
            {
                // Check if parameter includes channel (format: deviceId_chN)
                string deviceId = actionParameter;
                int channel = 0;

                if (actionParameter.Contains("_ch"))
                {
                    var parts = actionParameter.Split(new[] { "_ch" }, StringSplitOptions.None);
                    if (parts.Length == 2)
                    {
                        deviceId = parts[0];
                        int.TryParse(parts[1], out channel);
                        DebugLogger.Log($"  -> Device action for device ID: {deviceId}, channel: {channel}");
                    }
                }
                else
                {
                    DebugLogger.Log($"  -> Device action for device ID: {deviceId}");
                }

                await ToggleDeviceAsync(deviceId, channel, skipStatusRefresh: false);
            }

            DebugLogger.Log($"  -> Calling ActionImageChanged");
            ActionImageChanged(actionParameter);
        }

        private async Task ToggleDeviceAsync(string deviceId, int channel, bool skipStatusRefresh = false)
        {
            DebugLogger.Log($"    -> ToggleDeviceAsync called for device: {deviceId}, channel: {channel}, skipRefresh: {skipStatusRefresh}");

            var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
            if (device == null)
            {
                DebugLogger.Log($"    -> ERROR: Device {deviceId} not found in plugin devices list!");
                return;
            }

            DebugLogger.Log($"    -> Found device: {device.Name}");
            var deviceType = device.GetDeviceType();
            var isOn = GetDeviceState(device, channel);
            DebugLogger.Log($"    -> Current state: {(isOn ? "ON" : "OFF")}, toggling to: {(isOn ? "OFF" : "ON")}");
            DebugLogger.Log($"    -> Device type: {deviceType}");

            // Use device-specific API methods based on type
            bool success = false;
            bool isGen3 = device.Switch0 != null || device.Sys != null;

            if (deviceType == ShellyDeviceType.RGBW || deviceType == ShellyDeviceType.Dimmer)
            {
                DebugLogger.Log($"    -> RGBW/Dimmer device: Calling SetLightStateAsync with channel={channel}...");
                success = await _plugin.ApiClient.SetLightStateAsync(deviceId, channel, !isOn);
            }
            else if (isGen3)
            {
                DebugLogger.Log($"    -> Gen3 device: Calling SetGen3SwitchStateAsync with channel={channel}...");
                success = await _plugin.ApiClient.SetGen3SwitchStateAsync(deviceId, channel, !isOn);
            }
            else
            {
                DebugLogger.Log($"    -> Standard relay device: Calling SetRelayStateAsync with channel={channel}...");
                success = await _plugin.ApiClient.SetRelayStateAsync(deviceId, channel, !isOn);
            }
            DebugLogger.Log($"    -> API call completed, success = {success}");

            if (!success)
            {
                DebugLogger.Log($"    -> WARNING: API call failed for device {deviceId}!");
            }

            // Skip status refresh for group operations (to prevent rate limit)
            // Background refresh task will update status anyway
            if (skipStatusRefresh)
            {
                DebugLogger.Log($"    -> Skipping status refresh (group operation)");
                return;
            }

            // Refresh device status for single device operations
            DebugLogger.Log($"    -> Waiting 500ms before status refresh...");
            await Task.Delay(500);
            DebugLogger.Log($"    -> Getting updated device status...");
            var updatedDevice = await _plugin.ApiClient.GetDeviceStatusAsync(deviceId);
            if (updatedDevice != null)
            {
                DebugLogger.Log($"    -> Got updated status, updating device in list");
                var index = _plugin.Devices.FindIndex(d => d.Id == deviceId);
                if (index >= 0)
                {
                    _plugin.Devices[index] = updatedDevice;
                    DebugLogger.Log($"    -> Device updated at index {index}");
                }
                else
                {
                    DebugLogger.Log($"    -> WARNING: Could not find device index to update!");
                }
            }
            else
            {
                DebugLogger.Log($"    -> WARNING: GetDeviceStatusAsync returned null!");
            }
        }

        private bool GetDeviceState(ShellyDevice device, int channel = 0)
        {
            // Check Gen 3 devices first
            if (device.Switch0 != null && channel == 0)
            {
                var isOn = device.Switch0.Output;
                DebugLogger.Log($"      -> GetDeviceState: Using Gen3 Switch0 output = {isOn}");
                return isOn;
            }

            // Check Gen 1/2 devices
            if (device.Status?.Relays != null && device.Status.Relays.Count > channel)
            {
                var isOn = device.Status.Relays[channel].IsOn;
                DebugLogger.Log($"      -> GetDeviceState: Using Relay[{channel}] state = {isOn}");
                return isOn;
            }
            if (device.Status?.Lights != null && device.Status.Lights.Count > channel)
            {
                var isOn = device.Status.Lights[channel].IsOn;
                DebugLogger.Log($"      -> GetDeviceState: Using Light[{channel}] state = {isOn}");
                return isOn;
            }

            // Fallback for Gen 1/2 devices with direct fields
            if (device.Relays != null && device.Relays.Count > channel)
            {
                var isOn = device.Relays[channel].IsOn;
                DebugLogger.Log($"      -> GetDeviceState: Using direct Relays[{channel}] field = {isOn}");
                return isOn;
            }
            if (device.Lights != null && device.Lights.Count > channel)
            {
                var isOn = device.Lights[channel].IsOn;
                DebugLogger.Log($"      -> GetDeviceState: Using direct Lights[{channel}] field = {isOn}");
                return isOn;
            }

            DebugLogger.Log($"      -> GetDeviceState: No status available for channel {channel}, defaulting to false");
            return false;
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            if (string.IsNullOrEmpty(actionParameter))
            {
                using (var bitmapBuilder = new BitmapBuilder(imageSize))
                {
                    bitmapBuilder.Clear(BitmapColor.Black);
                    bitmapBuilder.DrawText("Switch");
                    return bitmapBuilder.ToImage();
                }
            }

            string deviceName;
            bool isOn = false;
            int channel = 0;

            if (actionParameter.StartsWith("group_"))
            {
                var groupId = actionParameter.Substring(6);
                var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);
                deviceName = group?.Name ?? "Unknown";

                // Check if any device in group is on
                if (group != null)
                {
                    foreach (var deviceId in group.DeviceIds)
                    {
                        var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
                        if (device != null && GetDeviceState(device, 0))
                        {
                            isOn = true;
                            break;
                        }
                    }
                }
            }
            else
            {
                // Check if parameter includes channel (format: deviceId_chN)
                string deviceId = actionParameter;

                if (actionParameter.Contains("_ch"))
                {
                    var parts = actionParameter.Split(new[] { "_ch" }, StringSplitOptions.None);
                    if (parts.Length == 2)
                    {
                        deviceId = parts[0];
                        int.TryParse(parts[1], out channel);
                    }
                }

                var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
                if (device != null)
                {
                    deviceName = channel > 0 ? $"{device.Name} - Ch{channel + 1}" : device.Name;
                    isOn = GetDeviceState(device, channel);
                }
                else
                {
                    deviceName = "Unknown";
                }
            }

            using (var builder = new BitmapBuilder(imageSize))
            {
                builder.Clear(isOn ? new BitmapColor(0, 200, 0) : BitmapColor.Black);
                builder.DrawText(deviceName);

                return builder.ToImage();
            }
        }
    }
}
