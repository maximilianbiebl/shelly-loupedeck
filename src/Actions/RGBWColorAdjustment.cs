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
        private Dictionary<string, (int R, int G, int B, int W)> _presetColors = new Dictionary<string, (int, int, int, int)>
        {
            { "red", (255, 0, 0, 0) },
            { "green", (0, 255, 0, 0) },
            { "blue", (0, 0, 255, 0) },
            { "white", (0, 0, 0, 255) },
            { "yellow", (255, 255, 0, 0) },
            { "cyan", (0, 255, 255, 0) },
            { "magenta", (255, 0, 255, 0) },
            { "warm_white", (255, 200, 150, 100) },
            { "cool_white", (200, 220, 255, 100) }
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
                if (group.Type == ShellyDeviceType.RGBW)
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
                DebugLogger.Log("  -> Parameter is null or empty, returning");
                return;
            }

            // Find which color key the parameter ends with
            string colorKey = null;
            string devicePart = null;

            foreach (var key in _presetColors.Keys)
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
                DebugLogger.Log($"  -> No valid color key found in parameter");
                return;
            }

            DebugLogger.Log($"  -> Color key: {colorKey}");
            DebugLogger.Log($"  -> Device part: {devicePart}");

            var color = _presetColors[colorKey];
            DebugLogger.Log($"  -> Color values: R={color.R}, G={color.G}, B={color.B}, W={color.W}");

            if (devicePart.StartsWith("group_"))
            {
                // Format: group_{groupId}
                var groupId = devicePart.Substring(6);
                DebugLogger.Log($"  -> Group action for group ID: {groupId}");
                var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);
                if (group != null)
                {
                    DebugLogger.Log($"  -> Found group with {group.DeviceIds.Count} devices");
                    foreach (var deviceId in group.DeviceIds)
                    {
                        DebugLogger.Log($"    -> Setting color for device: {deviceId}");
                        await _plugin.ApiClient.SetLightColorAsync(deviceId, color.R, color.G, color.B, color.W);
                    }
                }
                else
                {
                    DebugLogger.Log($"  -> Group not found!");
                }
            }
            else
            {
                // Format: {deviceId}
                DebugLogger.Log($"  -> Device action for device ID: {devicePart}");
                await _plugin.ApiClient.SetLightColorAsync(devicePart, color.R, color.G, color.B, color.W);
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
