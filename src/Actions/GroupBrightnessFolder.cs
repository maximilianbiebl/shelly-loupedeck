using System;
using System.Collections.Generic;
using System.Linq;
using Loupedeck;
using ShellyLoupedeckPlugin.Models;

namespace ShellyLoupedeckPlugin.Actions
{
    public class GroupBrightnessFolder : PluginDynamicFolder
    {
        private ShellyLoupedeckPlugin _plugin;
        private Dictionary<string, int> _brightnessPresets = new Dictionary<string, int>
        {
            { "10%", 10 },
            { "25%", 25 },
            { "50%", 50 },
            { "75%", 75 },
            { "100%", 100 }
        };

        public GroupBrightnessFolder()
        {
            DisplayName = "Group Brightness";
            Description = "Brightness presets for groups";
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

            // Add a folder for each Brightness/Dimmer group
            foreach (var group in _plugin.Groups)
            {
                if (group.Purpose == GroupPurpose.Brightness || group.Purpose == GroupPurpose.Color)
                {
                    // Create a folder for this group
                    var folderId = $"group_{group.Id}";
                    AddParameter(folderId, $"{group.Name}", group.Name);

                    // Add brightness preset buttons inside this folder
                    foreach (var preset in _brightnessPresets)
                    {
                        var presetParamId = $"{folderId}_{preset.Key}";
                        AddParameter(presetParamId, preset.Key, group.Name, folderId);
                    }
                }
            }

            DebugLogger.Log($"GroupBrightnessFolder: Created {_plugin.Groups.Count(g => g.Purpose == GroupPurpose.Brightness || g.Purpose == GroupPurpose.Color)} folder parameters");
        }

        public override async void RunCommand(string actionParameter)
        {
            DebugLogger.Log($"GroupBrightnessFolder: RunCommand called with parameter: {actionParameter}");

            if (string.IsNullOrEmpty(actionParameter))
            {
                DebugLogger.Log("  -> Parameter is null or empty, returning");
                return;
            }

            // Check if this is a folder click (just group_{groupId})
            if (actionParameter.StartsWith("group_") && !actionParameter.Contains("%"))
            {
                DebugLogger.Log("  -> Folder opened, no action needed");
                return;
            }

            // Parse parameter: group_{groupId}_{brightness}
            var parts = actionParameter.Split('_');
            if (parts.Length < 3)
            {
                DebugLogger.Log("  -> Invalid parameter format");
                return;
            }

            var groupId = parts[1]; // parts[0] = "group", parts[1] = groupId
            var brightnessKey = parts[2]; // parts[2] = brightness preset (e.g., "10%")

            DebugLogger.Log($"  -> Group ID: {groupId}");
            DebugLogger.Log($"  -> Brightness key: {brightnessKey}");

            if (!_brightnessPresets.ContainsKey(brightnessKey))
            {
                DebugLogger.Log($"  -> No valid brightness preset found");
                return;
            }

            var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);
            if (group == null)
            {
                DebugLogger.Log($"  -> Group not found!");
                return;
            }

            var targetBrightness = _brightnessPresets[brightnessKey];
            DebugLogger.Log($"  -> Target brightness: {targetBrightness}%");
            DebugLogger.Log($"  -> Setting brightness for group '{group.Name}' with {group.DeviceIds.Count} devices");

            // For color groups, preserve the color
            (int R, int G, int B, int W, int? temp) groupColorState = (0, 0, 0, 0, null);
            if (group.Purpose == GroupPurpose.Color)
            {
                if (group.DeviceIds.Count > 0 && _plugin.DeviceColorStates.ContainsKey(group.DeviceIds[0]))
                {
                    groupColorState = _plugin.DeviceColorStates[group.DeviceIds[0]];
                    DebugLogger.Log($"  -> Using color from first device: RGB=({groupColorState.R},{groupColorState.G},{groupColorState.B})");
                }
                else
                {
                    groupColorState = (255, 180, 100, 0, null);
                    DebugLogger.Log($"  -> Using default warm white color");
                }
            }

            for (int i = 0; i < group.DeviceIds.Count; i++)
            {
                var deviceId = group.DeviceIds[i];
                DebugLogger.Log($"    -> Group device {i + 1}/{group.DeviceIds.Count}: {deviceId}");

                // Record user action before each device to prevent refresh task collision
                _plugin.RecordUserAction();

                bool success = false;

                if (group.Purpose == GroupPurpose.Color)
                {
                    // For color groups, sync color state and set brightness
                    _plugin.DeviceColorStates[deviceId] = groupColorState;

                    // Determine if we're in color or white mode
                    bool isColorMode = groupColorState.R > 0 || groupColorState.G > 0 || groupColorState.B > 0;

                    if (isColorMode)
                    {
                        // Color mode: use gain parameter
                        success = await _plugin.ApiClient.SetLightColorAsync(deviceId, groupColorState.R, groupColorState.G, groupColorState.B, groupColorState.W, null, groupColorState.temp, targetBrightness);
                    }
                    else
                    {
                        // White mode: use brightness parameter
                        success = await _plugin.ApiClient.SetLightColorAsync(deviceId, groupColorState.R, groupColorState.G, groupColorState.B, groupColorState.W, null, groupColorState.temp, targetBrightness);
                    }
                }
                else
                {
                    // For brightness/dimmer groups, just set brightness
                    success = await _plugin.ApiClient.SetLightBrightnessAsync(deviceId, targetBrightness);
                }

                if (success)
                {
                    _plugin.DeviceBrightnessCache[deviceId] = targetBrightness;
                    DebugLogger.Log($"    -> Successfully set brightness to {targetBrightness}%");
                }

                // Add 2 second delay between devices to respect rate limit
                if (i < group.DeviceIds.Count - 1)
                {
                    DebugLogger.Log($"    -> Waiting 2000ms before next device (rate limit prevention)");
                    await System.Threading.Tasks.Task.Delay(2000);
                }
            }

            DebugLogger.Log($"  -> Brightness change complete for group '{group.Name}'");
        }

        public override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            // If this is a folder (just group_{groupId}), show a folder icon
            if (actionParameter.StartsWith("group_") && !actionParameter.Contains("%"))
            {
                using (var bitmapBuilder = new BitmapBuilder(imageSize))
                {
                    bitmapBuilder.Clear(new BitmapColor(40, 40, 50));
                    bitmapBuilder.DrawText("📁");
                    return bitmapBuilder.ToImage();
                }
            }

            // For brightness presets, show the percentage
            var parts = actionParameter.Split('_');
            if (parts.Length >= 3)
            {
                var brightnessKey = parts[2];

                if (_brightnessPresets.ContainsKey(brightnessKey))
                {
                    var brightness = _brightnessPresets[brightnessKey];
                    using (var bitmapBuilder = new BitmapBuilder(imageSize))
                    {
                        // Gradient from dark to bright based on brightness
                        var grayValue = (byte)(brightness * 2.55);
                        bitmapBuilder.Clear(new BitmapColor(grayValue, grayValue, grayValue));

                        // Text color: black for bright backgrounds, white for dark
                        var textColor = brightness > 50 ? BitmapColor.Black : BitmapColor.White;
                        bitmapBuilder.DrawText(brightnessKey, textColor);

                        return bitmapBuilder.ToImage();
                    }
                }
            }

            return null;
        }
    }
}
