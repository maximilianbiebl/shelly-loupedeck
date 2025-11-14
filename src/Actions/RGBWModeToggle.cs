using System;
using System.Linq;
using System.Threading.Tasks;
using Loupedeck;
using ShellyLoupedeckPlugin.Models;

namespace ShellyLoupedeckPlugin.Actions
{
    public class RGBWModeToggle : PluginDynamicCommand
    {
        private ShellyLoupedeckPlugin _plugin;

        public RGBWModeToggle() : base()
        {
            DisplayName = "RGBW Mode Toggle";
            Description = "Switch RGBW lights between White and Color mode";
            GroupName = "RGBW Controls";
        }

        protected override bool OnLoad()
        {
            _plugin = (ShellyLoupedeckPlugin)Plugin;
            _plugin.DevicesUpdated += OnDevicesUpdated;
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
            CreateParameters();
        }

        private void CreateParameters()
        {
            RemoveAllParameters();

            // Add White/Color toggle for each RGBW device
            foreach (var device in _plugin.Devices)
            {
                if (device.GetDeviceType() == ShellyDeviceType.RGBW)
                {
                    AddParameter($"{device.Id}_white", $"{device.Name} - White", device.Name);
                    AddParameter($"{device.Id}_color", $"{device.Name} - Color", device.Name);
                }
            }

            // Add White/Color toggle for Color and Brightness groups
            foreach (var group in _plugin.Groups)
            {
                if (group.Purpose == GroupPurpose.Color || group.Purpose == GroupPurpose.Brightness)
                {
                    AddParameter($"group_{group.Id}_white", $"[Group] {group.Name} - White", "Groups");
                    AddParameter($"group_{group.Id}_color", $"[Group] {group.Name} - Color", "Groups");
                }
            }

            ActionImageChanged();
        }

        protected override async void RunCommand(string actionParameter)
        {
            DebugLogger.Log($"=== RGBWModeToggle: RunCommand called with parameter: {actionParameter} ===");

            if (string.IsNullOrEmpty(actionParameter))
            {
                DebugLogger.Log("  -> Parameter is null or empty, returning");
                return;
            }

            // Determine mode from parameter
            bool setToWhite = actionParameter.EndsWith("_white");
            string mode = setToWhite ? "white" : "color";

            // Remove mode suffix to get device/group ID
            string devicePart = actionParameter.Replace("_white", "").Replace("_color", "");

            DebugLogger.Log($"  -> Setting mode to: {mode.ToUpper()}");

            // Record user action to prevent refresh conflicts
            _plugin.RecordUserAction();

            if (devicePart.StartsWith("group_"))
            {
                var groupId = devicePart.Substring(6);
                DebugLogger.Log($"  -> Group action for group ID: {groupId}");
                var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);
                if (group != null)
                {
                    DebugLogger.Log($"  -> Found group with {group.DeviceIds.Count} devices, setting mode sequentially");
                    for (int i = 0; i < group.DeviceIds.Count; i++)
                    {
                        var deviceId = group.DeviceIds[i];

                        // Record user action before each device to prevent refresh task collision
                        _plugin.RecordUserAction();

                        await SetDeviceModeAsync(deviceId, setToWhite);

                        // Add 2 second delay between devices to respect rate limit (except after last device)
                        if (i < group.DeviceIds.Count - 1)
                        {
                            DebugLogger.Log($"  -> Waiting 2000ms before next device (rate limit prevention)");
                            await Task.Delay(2000);
                        }
                    }
                }
            }
            else
            {
                await SetDeviceModeAsync(devicePart, setToWhite);
            }

            ActionImageChanged(actionParameter);
        }

        private async Task SetDeviceModeAsync(string deviceId, bool setToWhite)
        {
            DebugLogger.Log($"  -> Setting device {deviceId} to {(setToWhite ? "WHITE" : "COLOR")} mode");

            // Get current brightness
            int brightness = 100;
            if (_plugin.DeviceBrightnessCache.ContainsKey(deviceId))
            {
                brightness = _plugin.DeviceBrightnessCache[deviceId];
                DebugLogger.Log($"  -> Using cached brightness: {brightness}%");
            }
            else
            {
                var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
                if (device?.Status?.Lights != null && device.Status.Lights.Count > 0)
                {
                    brightness = device.Status.Lights[0].Brightness;
                    DebugLogger.Log($"  -> Using device brightness: {brightness}%");
                }
            }

            if (setToWhite)
            {
                // Set to white mode with warm white color temperature (3000K)
                DebugLogger.Log($"  -> Setting to WHITE mode: W=255, temp=3000K, brightness={brightness}%");
                var success = await _plugin.ApiClient.SetLightColorAsync(deviceId, 0, 0, 0, 255, null, 3000, brightness);

                if (success)
                {
                    _plugin.DeviceColorStates[deviceId] = (0, 0, 0, 255, 3000);
                    _plugin.DeviceBrightnessCache[deviceId] = brightness;
                    DebugLogger.Log($"  -> Updated cache for white mode");
                }
            }
            else
            {
                // Set to color mode with warm white RGB equivalent
                DebugLogger.Log($"  -> Setting to COLOR mode: RGB=(255,180,100), brightness={brightness}%");
                var success = await _plugin.ApiClient.SetLightColorAsync(deviceId, 255, 180, 100, 0, null, null, brightness);

                if (success)
                {
                    _plugin.DeviceColorStates[deviceId] = (255, 180, 100, 0, null);
                    _plugin.DeviceBrightnessCache[deviceId] = brightness;
                    DebugLogger.Log($"  -> Updated cache for color mode");
                }
            }
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            if (string.IsNullOrEmpty(actionParameter))
            {
                using (var builder = new BitmapBuilder(imageSize))
                {
                    builder.Clear(BitmapColor.Black);
                    builder.DrawText("Mode");
                    return builder.ToImage();
                }
            }

            bool isWhite = actionParameter.EndsWith("_white");
            string devicePart = actionParameter.Replace("_white", "").Replace("_color", "");

            string deviceName;
            if (devicePart.StartsWith("group_"))
            {
                var groupId = devicePart.Substring(6);
                var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);
                deviceName = group?.Name ?? "Unknown";
            }
            else
            {
                var device = _plugin.Devices.FirstOrDefault(d => d.Id == devicePart);
                deviceName = device?.Name ?? "Unknown";
            }

            using (var builder = new BitmapBuilder(imageSize))
            {
                if (isWhite)
                {
                    builder.Clear(new BitmapColor(255, 255, 255));
                    builder.DrawText(deviceName, BitmapColor.Black, 10);
                    builder.DrawText("WHITE", BitmapColor.Black, 30);
                }
                else
                {
                    builder.Clear(new BitmapColor(255, 100, 0));
                    builder.DrawText(deviceName, BitmapColor.White, 10);
                    builder.DrawText("COLOR", BitmapColor.White, 30);
                }

                return builder.ToImage();
            }
        }
    }
}
