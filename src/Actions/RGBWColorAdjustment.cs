using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Loupedeck;
using ShellyLoupedeckPlugin.Models;

namespace ShellyLoupedeckPlugin.Actions
{
    public class RGBWColorAdjustment : PluginDynamicCommand
    {
        private ShellyLoupedeckPlugin _plugin;
        private Dictionary<string, bool> _groupOperationInProgress = new Dictionary<string, bool>();
        private readonly object _operationLock = new object();
        private Dictionary<string, (int R, int G, int B, int W)> _presetColors = new Dictionary<string, (int, int, int, int)>
        {
            { "red", (255, 0, 0, 0) },
            { "green", (0, 255, 0, 0) },
            { "blue", (0, 0, 255, 0) },
            { "white", (0, 0, 0, 255) },
            { "yellow", (255, 255, 0, 0) },
            { "cyan", (0, 255, 255, 0) },
            { "magenta", (255, 0, 255, 0) },
            { "warm_white", (0, 0, 0, 255) },
            { "cool_white", (0, 0, 0, 255) }
        };

        // Color temperature in Kelvin for white modes
        private Dictionary<string, int> _colorTemperatures = new Dictionary<string, int>
        {
            { "white", 4750 },        // Neutral white
            { "warm_white", 3000 },   // Warm white
            { "cool_white", 6500 }    // Cool white (like daylight)
        };

        public RGBWColorAdjustment() : base()
        {
            DisplayName = "RGBW Color";
            Description = "Set color of RGBW bulbs";
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

            // Add color presets for each device
            foreach (var device in _plugin.Devices)
            {
                if (device.GetDeviceType() == ShellyDeviceType.RGBW)
                {
                    foreach (var color in _presetColors)
                    {
                        AddParameter($"{device.Id}_{color.Key}", $"{device.Name} - {color.Key}", device.Name);
                    }
                }
            }

            // Add color presets for each group
            foreach (var group in _plugin.Groups)
            {
                if (group.Purpose == GroupPurpose.Color)
                {
                    foreach (var color in _presetColors)
                    {
                        AddParameter($"group_{group.Id}_{color.Key}", $"[Group] {group.Name} - {color.Key}", group.Name);
                    }
                }
            }

            ActionImageChanged();
        }

        protected override async void RunCommand(string actionParameter)
        {
            DebugLogger.Log($"=== RGBWColorAdjustment: RunCommand called with parameter: {actionParameter} ===");

            if (string.IsNullOrEmpty(actionParameter))
            {
                DebugLogger.Verbose("  -> Parameter is null or empty, returning");
                return;
            }

            // Record user action to prevent refresh conflicts
            _plugin.RecordUserAction();

            // Find which color key the parameter ends with
            // Sort keys by length descending to match longest keys first (e.g., "cool_white" before "white")
            string colorKey = null;
            string devicePart = null;

            foreach (var key in _presetColors.Keys.OrderByDescending(k => k.Length))
            {
                if (actionParameter.EndsWith("_" + key))
                {
                    colorKey = key;
                    devicePart = actionParameter.Substring(0, actionParameter.Length - key.Length - 1);
                    break;
                }
            }

            if (colorKey == null)
            {
                DebugLogger.Verbose($"  -> No valid color key found in parameter");
                return;
            }

            DebugLogger.Verbose($"  -> Color key: {colorKey}");
            DebugLogger.Verbose($"  -> Device part: {devicePart}");

            var color = _presetColors[colorKey];
            DebugLogger.Verbose($"  -> Color values: R={color.R}, G={color.G}, B={color.B}, W={color.W}");

            // Get color temperature if this is a white mode
            int? temperature = null;
            if (_colorTemperatures.ContainsKey(colorKey))
            {
                temperature = _colorTemperatures[colorKey];
                DebugLogger.Verbose($"  -> Color temperature: {temperature}K");
            }

            if (devicePart.StartsWith("group_"))
            {
                // Check if group operation is already in progress
                lock (_operationLock)
                {
                    if (_groupOperationInProgress.ContainsKey(devicePart) && _groupOperationInProgress[devicePart])
                    {
                        DebugLogger.Verbose($"  -> Group operation already in progress for {devicePart}, skipping this request to prevent rate limit");
                        return;
                    }
                    _groupOperationInProgress[devicePart] = true;
                }

                try
                {
                    // Format: group_{groupId}
                    var groupId = devicePart.Substring(6);
                    DebugLogger.Verbose($"  -> Group action for group ID: {groupId}");
                    var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);
                    if (group != null)
                    {
                        DebugLogger.Verbose($"  -> Found group with {group.DeviceIds.Count} devices, calling sequentially to avoid rate limit");
                        for (int i = 0; i < group.DeviceIds.Count; i++)
                        {
                            var deviceId = group.DeviceIds[i];
                            DebugLogger.Verbose($"    -> Group device {i+1}/{group.DeviceIds.Count}: Setting color for {deviceId}");

                            // Record user action before each device to prevent refresh task collision
                            _plugin.RecordUserAction();

                            // Always preserve current brightness (both color and white mode)
                            int? brightnessToSet = null;
                            bool isColorMode = color.R > 0 || color.G > 0 || color.B > 0;
                            string modeStr = isColorMode ? "COLOR" : "WHITE";

                            // Read current brightness and set it explicitly
                            // (prevents API from using default value on mode switch)
                            if (_plugin.DeviceBrightnessCache.ContainsKey(deviceId))
                            {
                                brightnessToSet = _plugin.DeviceBrightnessCache[deviceId];
                                DebugLogger.Verbose($"    -> {modeStr} mode: Setting brightness to {brightnessToSet}% (from cache)");
                            }
                            else
                            {
                                // Fallback: read from device status
                                var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
                                if (device?.Status?.Lights != null && device.Status.Lights.Count > 0)
                                {
                                    brightnessToSet = device.Status.Lights[0].Brightness;
                                    DebugLogger.Verbose($"    -> {modeStr} mode: Setting brightness to {brightnessToSet}% (from device)");
                                }
                                else
                                {
                                    DebugLogger.Verbose($"    -> {modeStr} mode: No brightness info, not setting (device keeps current value)");
                                }
                            }

                            var success = await _plugin.ApiClient.SetLightColorAsync(deviceId, color.R, color.G, color.B, color.W, null, temperature, brightnessToSet);

                            // Only update state if API call succeeded
                            if (success)
                            {
                                // Store color state in plugin for brightness adjustment
                                _plugin.DeviceColorStates[deviceId] = (color.R, color.G, color.B, color.W, temperature);
                                DebugLogger.Verbose($"    -> Stored color state for device {deviceId}: R={color.R}, G={color.G}, B={color.B}, W={color.W}, Temp={temperature}");

                                // Update brightness cache only if we set it
                                if (brightnessToSet.HasValue)
                                {
                                    _plugin.DeviceBrightnessCache[deviceId] = brightnessToSet.Value;
                                    DebugLogger.Verbose($"    -> Updated brightness cache to {brightnessToSet.Value}%");
                                }
                            }
                            else
                            {
                                DebugLogger.Verbose($"    -> API call failed, NOT updating color state cache");
                            }

                            // Add 2 second delay between devices to respect rate limit (except after last device)
                            if (i < group.DeviceIds.Count - 1)
                            {
                                DebugLogger.Verbose($"    -> Waiting 2000ms before next device (rate limit prevention)");
                                await Task.Delay(2000);
                            }
                        }
                    }
                    else
                    {
                        DebugLogger.Verbose($"  -> Group not found!");
                    }
                }
                finally
                {
                    // Always reset the flag when done
                    lock (_operationLock)
                    {
                        _groupOperationInProgress[devicePart] = false;
                    }
                }
            }
            else
            {
                // Format: {deviceId}
                DebugLogger.Verbose($"  -> Device action for device ID: {devicePart}");

                // Always preserve current brightness (both color and white mode)
                int? brightnessToSet = null;
                bool isColorMode = color.R > 0 || color.G > 0 || color.B > 0;
                string modeStr = isColorMode ? "COLOR" : "WHITE";

                // Read current brightness and set it explicitly
                // (prevents API from using default value on mode switch)
                if (_plugin.DeviceBrightnessCache.ContainsKey(devicePart))
                {
                    brightnessToSet = _plugin.DeviceBrightnessCache[devicePart];
                    DebugLogger.Verbose($"  -> {modeStr} mode: Setting brightness to {brightnessToSet}% (from cache)");
                }
                else
                {
                    // Fallback: read from device status
                    var device = _plugin.Devices.FirstOrDefault(d => d.Id == devicePart);
                    if (device?.Status?.Lights != null && device.Status.Lights.Count > 0)
                    {
                        brightnessToSet = device.Status.Lights[0].Brightness;
                        DebugLogger.Verbose($"  -> {modeStr} mode: Setting brightness to {brightnessToSet}% (from device)");
                    }
                    else
                    {
                        DebugLogger.Verbose($"  -> {modeStr} mode: No brightness info, not setting (device keeps current value)");
                    }
                }

                var success = await _plugin.ApiClient.SetLightColorAsync(devicePart, color.R, color.G, color.B, color.W, null, temperature, brightnessToSet);

                // Only update state if API call succeeded
                if (success)
                {
                    // Store color state in plugin for brightness adjustment
                    _plugin.DeviceColorStates[devicePart] = (color.R, color.G, color.B, color.W, temperature);
                    DebugLogger.Verbose($"  -> Stored color state for device {devicePart}: R={color.R}, G={color.G}, B={color.B}, W={color.W}, Temp={temperature}");

                    // Update brightness cache only if we set it
                    if (brightnessToSet.HasValue)
                    {
                        _plugin.DeviceBrightnessCache[devicePart] = brightnessToSet.Value;
                        DebugLogger.Verbose($"  -> Updated brightness cache to {brightnessToSet.Value}%");
                    }
                }
                else
                {
                    DebugLogger.Verbose($"  -> API call failed, NOT updating color state cache");
                }
            }
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            if (string.IsNullOrEmpty(actionParameter))
            {
                using (var bitmapBuilder = new BitmapBuilder(imageSize))
                {
                    bitmapBuilder.Clear(BitmapColor.Black);
                    bitmapBuilder.DrawText("Color");
                    return bitmapBuilder.ToImage();
                }
            }

            var parts = actionParameter.Split('_');
            var colorKey = parts[parts.Length - 1];

            if (!_presetColors.ContainsKey(colorKey))
            {
                using (var bitmapBuilder = new BitmapBuilder(imageSize))
                {
                    bitmapBuilder.Clear(BitmapColor.Black);
                    return bitmapBuilder.ToImage();
                }
            }

            var color = _presetColors[colorKey];
            var bitmapColor = new BitmapColor((byte)color.R, (byte)color.G, (byte)color.B);

            using (var builder = new BitmapBuilder(imageSize))
            {
                builder.Clear(bitmapColor);
                builder.DrawText(colorKey.Replace("_", " "), BitmapColor.White);

                return builder.ToImage();
            }
        }
    }
}
