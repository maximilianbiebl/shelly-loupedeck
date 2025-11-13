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
                    deviceType == ShellyDeviceType.RGBW)
                {
                    AddParameter(device.Id, device.Name, "Devices");
                    DebugLogger.Log($"    -> Added as parameter");
                    deviceCount++;
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
                if (group.Type == ShellyDeviceType.Switch ||
                    group.Type == ShellyDeviceType.ShellyPlus2PM ||
                    group.Type == ShellyDeviceType.RGBW)
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
                    DebugLogger.Log($"  -> Found group with {group.DeviceIds.Count} devices");
                    foreach (var deviceId in group.DeviceIds)
                    {
                        await ToggleDeviceAsync(deviceId);
                    }
                }
                else
                {
                    DebugLogger.Log($"  -> Group not found!");
                }
            }
            else
            {
                DebugLogger.Log($"  -> Device action for device ID: {actionParameter}");
                await ToggleDeviceAsync(actionParameter);
            }

            DebugLogger.Log($"  -> Calling ActionImageChanged");
            ActionImageChanged(actionParameter);
        }

        private async Task ToggleDeviceAsync(string deviceId)
        {
            DebugLogger.Log($"    -> ToggleDeviceAsync called for device: {deviceId}");

            var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
            if (device == null)
            {
                DebugLogger.Log($"    -> ERROR: Device {deviceId} not found in plugin devices list!");
                return;
            }

            DebugLogger.Log($"    -> Found device: {device.Name}");
            var deviceType = device.GetDeviceType();
            var isOn = GetDeviceState(device);
            DebugLogger.Log($"    -> Current state: {(isOn ? "ON" : "OFF")}, toggling to: {(isOn ? "OFF" : "ON")}");
            DebugLogger.Log($"    -> Device type: {deviceType}");

            bool success = false;
            if (deviceType == ShellyDeviceType.RGBW)
            {
                DebugLogger.Log($"    -> Calling API SetLightStateAsync...");
                success = await _plugin.ApiClient.SetLightStateAsync(deviceId, 0, !isOn);
            }
            else
            {
                DebugLogger.Log($"    -> Calling API SetRelayStateAsync...");
                success = await _plugin.ApiClient.SetRelayStateAsync(deviceId, 0, !isOn);
            }
            DebugLogger.Log($"    -> API call completed, success = {success}");

            // Refresh device status
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

        private bool GetDeviceState(ShellyDevice device)
        {
            // Check Gen 3 devices first
            if (device.Switch0 != null)
            {
                var isOn = device.Switch0.Output;
                DebugLogger.Log($"      -> GetDeviceState: Using Gen3 Switch0 output = {isOn}");
                return isOn;
            }

            // Check Gen 1/2 devices
            if (device.Status?.Relays != null && device.Status.Relays.Count > 0)
            {
                var isOn = device.Status.Relays[0].IsOn;
                DebugLogger.Log($"      -> GetDeviceState: Using Relay state = {isOn}");
                return isOn;
            }
            if (device.Status?.Lights != null && device.Status.Lights.Count > 0)
            {
                var isOn = device.Status.Lights[0].IsOn;
                DebugLogger.Log($"      -> GetDeviceState: Using Light state = {isOn}");
                return isOn;
            }

            // Fallback for Gen 1/2 devices with direct fields
            if (device.Relays != null && device.Relays.Count > 0)
            {
                var isOn = device.Relays[0].IsOn;
                DebugLogger.Log($"      -> GetDeviceState: Using direct Relays field = {isOn}");
                return isOn;
            }
            if (device.Lights != null && device.Lights.Count > 0)
            {
                var isOn = device.Lights[0].IsOn;
                DebugLogger.Log($"      -> GetDeviceState: Using direct Lights field = {isOn}");
                return isOn;
            }

            DebugLogger.Log($"      -> GetDeviceState: No status available, defaulting to false");
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
                        if (device != null && GetDeviceState(device))
                        {
                            isOn = true;
                            break;
                        }
                    }
                }
            }
            else
            {
                var device = _plugin.Devices.FirstOrDefault(d => d.Id == actionParameter);
                deviceName = device?.Name ?? "Unknown";
                if (device != null)
                {
                    isOn = GetDeviceState(device);
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
