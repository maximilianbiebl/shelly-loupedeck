using System;
using System.Collections.Generic;
using System.Linq;
using Loupedeck;
using ShellyLoupedeckPlugin.Models;

namespace ShellyLoupedeckPlugin.Actions
{
    public class GroupColorFolder : PluginDynamicFolder
    {
        private ShellyLoupedeckPlugin _plugin;
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

        public GroupColorFolder()
        {
            DisplayName = "Group Colors";
            Description = "Color folder for groups";
            GroupName = "Group Folders";
        }

        public override bool Load()
        {
            _plugin = (ShellyLoupedeckPlugin)base.Plugin;
            _plugin.DevicesUpdated += OnDevicesUpdated;
            _plugin.GroupsUpdated += OnGroupsUpdated;

            CreateParameters();

            return base.Load();
        }

        public override bool Unload()
        {
            _plugin.DevicesUpdated -= OnDevicesUpdated;
            _plugin.GroupsUpdated -= OnGroupsUpdated;

            return base.Unload();
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

            // Add a folder for each Color group
            foreach (var group in _plugin.Groups)
            {
                if (group.Purpose == GroupPurpose.Color || group.Purpose == GroupPurpose.Brightness)
                {
                    // Create a folder for this group
                    var folderId = $"group_{group.Id}";
                    AddParameter(folderId, $"{group.Name}", group.Name);

                    // Add color buttons inside this folder
                    foreach (var color in _presetColors)
                    {
                        var colorParamId = $"{folderId}_{color.Key}";
                        var colorName = color.Key.Replace("_", " ");
                        AddParameter(colorParamId, colorName, group.Name, folderId);
                    }
                }
            }

            DebugLogger.Log($"GroupColorFolder: Created {_plugin.Groups.Count(g => g.Purpose == GroupPurpose.Color || g.Purpose == GroupPurpose.Brightness)} folder parameters");
        }

        public override async void RunCommand(string actionParameter)
        {
            DebugLogger.Log($"GroupColorFolder: RunCommand called with parameter: {actionParameter}");

            if (string.IsNullOrEmpty(actionParameter))
            {
                DebugLogger.Log("  -> Parameter is null or empty, returning");
                return;
            }

            // Check if this is a folder click (just open, don't execute)
            if (actionParameter.StartsWith("group_") && !actionParameter.Contains("_red") &&
                !actionParameter.Contains("_green") && !actionParameter.Contains("_blue") &&
                !actionParameter.Contains("_white") && !actionParameter.Contains("_yellow") &&
                !actionParameter.Contains("_cyan") && !actionParameter.Contains("_magenta") &&
                !actionParameter.Contains("_warm_white") && !actionParameter.Contains("_cool_white"))
            {
                DebugLogger.Log("  -> Folder opened, no action needed");
                return;
            }

            // Parse parameter: group_{groupId}_{colorKey}
            var parts = actionParameter.Split('_');
            if (parts.Length < 3)
            {
                DebugLogger.Log("  -> Invalid parameter format");
                return;
            }

            // Extract group ID and color key
            var groupId = parts[1]; // parts[0] = "group", parts[1] = groupId
            var colorKey = string.Join("_", parts.Skip(2)); // remaining parts = color key

            DebugLogger.Log($"  -> Group ID: {groupId}");
            DebugLogger.Log($"  -> Color key: {colorKey}");

            if (!_presetColors.ContainsKey(colorKey))
            {
                DebugLogger.Log($"  -> No valid color key found");
                return;
            }

            var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);
            if (group == null)
            {
                DebugLogger.Log($"  -> Group not found!");
                return;
            }

            var color = _presetColors[colorKey];
            DebugLogger.Log($"  -> Color values: R={color.R}, G={color.G}, B={color.B}, W={color.W}");

            // Get color temperature if this is a white mode
            int? temperature = null;
            if (_colorTemperatures.ContainsKey(colorKey))
            {
                temperature = _colorTemperatures[colorKey];
                DebugLogger.Log($"  -> Color temperature: {temperature}K");
            }

            DebugLogger.Log($"  -> Setting color for group '{group.Name}' with {group.DeviceIds.Count} devices");

            for (int i = 0; i < group.DeviceIds.Count; i++)
            {
                var deviceId = group.DeviceIds[i];
                DebugLogger.Log($"    -> Group device {i + 1}/{group.DeviceIds.Count}: Setting color for {deviceId}");

                // Record user action before each device to prevent refresh task collision
                _plugin.RecordUserAction();

                // Always preserve current brightness
                int? brightnessToSet = null;
                bool isColorMode = color.R > 0 || color.G > 0 || color.B > 0;
                string modeStr = isColorMode ? "COLOR" : "WHITE";

                if (_plugin.DeviceBrightnessCache.ContainsKey(deviceId))
                {
                    brightnessToSet = _plugin.DeviceBrightnessCache[deviceId];
                    DebugLogger.Log($"    -> {modeStr} mode: Setting brightness to {brightnessToSet}% (from cache)");
                }
                else
                {
                    var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
                    if (device?.Status?.Lights != null && device.Status.Lights.Count > 0)
                    {
                        brightnessToSet = device.Status.Lights[0].Brightness;
                        DebugLogger.Log($"    -> {modeStr} mode: Setting brightness to {brightnessToSet}% (from device)");
                    }
                }

                var success = await _plugin.ApiClient.SetLightColorAsync(deviceId, color.R, color.G, color.B, color.W, null, temperature, brightnessToSet);

                if (success)
                {
                    _plugin.DeviceColorStates[deviceId] = (color.R, color.G, color.B, color.W, temperature);
                    if (brightnessToSet.HasValue)
                    {
                        _plugin.DeviceBrightnessCache[deviceId] = brightnessToSet.Value;
                    }
                }

                // Add 2 second delay between devices to respect rate limit
                if (i < group.DeviceIds.Count - 1)
                {
                    DebugLogger.Log($"    -> Waiting 2000ms before next device (rate limit prevention)");
                    await System.Threading.Tasks.Task.Delay(2000);
                }
            }

            DebugLogger.Log($"  -> Color change complete for group '{group.Name}'");
        }

        public override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            // If this is a folder (group), show a folder icon
            if (actionParameter.StartsWith("group_") && !actionParameter.Contains("_red") &&
                !actionParameter.Contains("_green") && !actionParameter.Contains("_blue") &&
                !actionParameter.Contains("_white") && !actionParameter.Contains("_yellow") &&
                !actionParameter.Contains("_cyan") && !actionParameter.Contains("_magenta") &&
                !actionParameter.Contains("_warm_white") && !actionParameter.Contains("_cool_white"))
            {
                using (var bitmapBuilder = new BitmapBuilder(imageSize))
                {
                    bitmapBuilder.Clear(new BitmapColor(40, 40, 50));
                    bitmapBuilder.DrawText("📁");
                    return bitmapBuilder.ToImage();
                }
            }

            // Extract color key from parameter
            var parts = actionParameter.Split('_');
            if (parts.Length >= 3)
            {
                var colorKey = string.Join("_", parts.Skip(2));

                if (_presetColors.ContainsKey(colorKey))
                {
                    var color = _presetColors[colorKey];
                    using (var bitmapBuilder = new BitmapBuilder(imageSize))
                    {
                        // Draw color circle
                        if (color.R > 0 || color.G > 0 || color.B > 0)
                        {
                            bitmapBuilder.Clear(new BitmapColor(color.R, color.G, color.B));
                        }
                        else
                        {
                            // White mode - show white
                            bitmapBuilder.Clear(new BitmapColor(255, 255, 255));
                        }

                        return bitmapBuilder.ToImage();
                    }
                }
            }

            return null;
        }
    }
}
