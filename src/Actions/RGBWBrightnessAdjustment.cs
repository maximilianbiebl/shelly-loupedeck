using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Loupedeck;
using ShellyLoupedeckPlugin.Models;

namespace ShellyLoupedeckPlugin.Actions
{
    public class RGBWBrightnessAdjustment : PluginDynamicAdjustment
    {
        private ShellyLoupedeckPlugin _plugin;
        private Dictionary<string, int> _currentBrightness = new Dictionary<string, int>();
        private Dictionary<string, Timer> _debounceTimers = new Dictionary<string, Timer>();
        private readonly object _timerLock = new object();

        public RGBWBrightnessAdjustment() : base(false)
        {
            DisplayName = "RGBW Brightness";
            Description = "Adjust brightness of RGBW bulbs";
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

            // Dispose all timers
            lock (_timerLock)
            {
                foreach (var timer in _debounceTimers.Values)
                {
                    timer?.Dispose();
                }
                _debounceTimers.Clear();
            }

            return base.OnUnload();
        }

        private void OnDevicesUpdated(object sender, EventArgs e)
        {
            CreateParameters();
            UpdateBrightnessValues();
        }

        private void CreateParameters()
        {
            RemoveAllParameters();

            // Add individual RGBW devices
            foreach (var device in _plugin.Devices)
            {
                if (device.GetDeviceType() == ShellyDeviceType.RGBW)
                {
                    AddParameter(device.Id, device.Name, "Devices");
                }
            }

            // Add RGBW groups
            foreach (var group in _plugin.Groups)
            {
                if (group.Type == ShellyDeviceType.RGBW)
                {
                    AddParameter($"group_{group.Id}", $"[Group] {group.Name}", "Groups");
                }
            }

            AdjustmentValueChanged();
        }

        private void UpdateBrightnessValues()
        {
            foreach (var device in _plugin.Devices)
            {
                if (device.GetDeviceType() == ShellyDeviceType.RGBW &&
                    device.Status?.Lights != null &&
                    device.Status.Lights.Count > 0)
                {
                    var light = device.Status.Lights[0];
                    _currentBrightness[device.Id] = light.Brightness;

                    // Initialize color state if not yet tracked (default to white mode)
                    if (!_plugin.DeviceColorStates.ContainsKey(device.Id))
                    {
                        _plugin.DeviceColorStates[device.Id] = (0, 0, 0, 255, null);
                        DebugLogger.Log($"  Initialized color state for device {device.Id} to white mode");
                    }
                }
            }
        }

        protected override void ApplyAdjustment(string actionParameter, int diff)
        {
            DebugLogger.Log($"=== RGBWBrightnessAdjustment: ApplyAdjustment called with parameter: {actionParameter}, diff: {diff} ===");

            if (string.IsNullOrEmpty(actionParameter))
            {
                DebugLogger.Log("  -> Parameter is null or empty, returning");
                return;
            }

            // Update brightness value immediately for UI responsiveness
            if (actionParameter.StartsWith("group_"))
            {
                var groupId = actionParameter.Substring(6);
                DebugLogger.Log($"  -> Group adjustment for group ID: {groupId}");
                var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);
                if (group != null)
                {
                    DebugLogger.Log($"  -> Found group with {group.DeviceIds.Count} devices, updating local values");
                    foreach (var deviceId in group.DeviceIds)
                    {
                        UpdateBrightnessValue(deviceId, diff);
                    }
                }
                else
                {
                    DebugLogger.Log($"  -> Group not found!");
                }
            }
            else
            {
                DebugLogger.Log($"  -> Device adjustment for device ID: {actionParameter}");
                UpdateBrightnessValue(actionParameter, diff);
            }

            AdjustmentValueChanged(actionParameter);

            // Debounce the API call
            lock (_timerLock)
            {
                if (_debounceTimers.ContainsKey(actionParameter))
                {
                    _debounceTimers[actionParameter]?.Dispose();
                }

                DebugLogger.Log($"  -> Starting debounce timer (600ms) for parameter: {actionParameter}");
                _debounceTimers[actionParameter] = new Timer(async _ =>
                {
                    DebugLogger.Log($"  -> Debounce timer elapsed, sending API call for parameter: {actionParameter}");
                    await SendBrightnessUpdateAsync(actionParameter);

                    lock (_timerLock)
                    {
                        if (_debounceTimers.ContainsKey(actionParameter))
                        {
                            _debounceTimers[actionParameter]?.Dispose();
                            _debounceTimers.Remove(actionParameter);
                        }
                    }
                }, null, 600, Timeout.Infinite);
            }
        }

        private void UpdateBrightnessValue(string deviceId, int diff)
        {
            if (!_currentBrightness.ContainsKey(deviceId))
            {
                var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
                if (device?.Status?.Lights != null && device.Status.Lights.Count > 0)
                {
                    _currentBrightness[deviceId] = device.Status.Lights[0].Brightness;
                    DebugLogger.Log($"    -> Initialized brightness from device status: {_currentBrightness[deviceId]}");
                }
                else
                {
                    _currentBrightness[deviceId] = 50;
                    DebugLogger.Log($"    -> Device status not available, defaulting to 50");
                }
            }

            var oldBrightness = _currentBrightness[deviceId];
            var newBrightness = Math.Max(0, Math.Min(100, _currentBrightness[deviceId] + diff));
            _currentBrightness[deviceId] = newBrightness;

            DebugLogger.Log($"    -> Brightness: {oldBrightness} -> {newBrightness} (diff={diff}, step=1)");
        }

        private async Task SendBrightnessUpdateAsync(string actionParameter)
        {
            if (actionParameter.StartsWith("group_"))
            {
                var groupId = actionParameter.Substring(6);
                var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);
                if (group != null)
                {
                    DebugLogger.Log($"  -> Sending brightness update for group with {group.DeviceIds.Count} devices");
                    foreach (var deviceId in group.DeviceIds)
                    {
                        await SendDeviceBrightnessAsync(deviceId);
                    }
                }
            }
            else
            {
                await SendDeviceBrightnessAsync(actionParameter);
            }
        }

        private async Task SendDeviceBrightnessAsync(string deviceId)
        {
            if (!_currentBrightness.ContainsKey(deviceId))
                return;

            var brightness = _currentBrightness[deviceId];

            // Check if device has a tracked color state
            if (_plugin.DeviceColorStates.ContainsKey(deviceId))
            {
                var colorState = _plugin.DeviceColorStates[deviceId];
                bool isColorMode = colorState.R > 0 || colorState.G > 0 || colorState.B > 0;

                if (isColorMode)
                {
                    // In color mode: send RGB values with brightness parameter
                    // The API should scale RGB values based on brightness
                    DebugLogger.Log($"    -> Device {deviceId} in COLOR mode: Sending RGB=({colorState.R},{colorState.G},{colorState.B}) with brightness={brightness}%");

                    // First set the color
                    await _plugin.ApiClient.SetLightColorAsync(deviceId, colorState.R, colorState.G, colorState.B, 0, null, null);

                    // Then set the brightness
                    await Task.Delay(100); // Small delay between API calls
                    await _plugin.ApiClient.SetLightBrightnessAsync(deviceId, brightness);
                    return;
                }
                else
                {
                    // In white mode: use brightness API
                    DebugLogger.Log($"    -> Device {deviceId} in WHITE mode: Setting brightness to {brightness}%");
                    await _plugin.ApiClient.SetLightBrightnessAsync(deviceId, brightness);

                    // If there's a temperature, reapply it after brightness change
                    if (colorState.Temperature.HasValue)
                    {
                        await Task.Delay(100); // Small delay between API calls
                        DebugLogger.Log($"    -> Reapplying color temperature: {colorState.Temperature}K");
                        await _plugin.ApiClient.SetLightColorAsync(deviceId, 0, 0, 0, 255, null, colorState.Temperature);
                    }
                    return;
                }
            }

            // Fallback: use simple brightness API
            DebugLogger.Log($"    -> No color state tracked for device {deviceId}, using simple brightness API: {brightness}%");
            await _plugin.ApiClient.SetLightBrightnessAsync(deviceId, brightness);
        }

        protected override string GetAdjustmentValue(string actionParameter)
        {
            if (string.IsNullOrEmpty(actionParameter))
                return "0%";

            int brightness = 0;

            if (actionParameter.StartsWith("group_"))
            {
                var groupId = actionParameter.Substring(6);
                var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);
                if (group != null && group.DeviceIds.Count > 0)
                {
                    var firstDeviceId = group.DeviceIds[0];
                    if (_currentBrightness.ContainsKey(firstDeviceId))
                    {
                        brightness = _currentBrightness[firstDeviceId];
                    }
                }
            }
            else if (_currentBrightness.ContainsKey(actionParameter))
            {
                brightness = _currentBrightness[actionParameter];
            }

            return $"{brightness}%";
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            var brightness = GetAdjustmentValue(actionParameter);

            string deviceName;
            if (string.IsNullOrEmpty(actionParameter))
            {
                deviceName = "RGBW";
            }
            else if (actionParameter.StartsWith("group_"))
            {
                var groupId = actionParameter.Substring(6);
                var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);
                deviceName = group?.Name ?? "Unknown";
            }
            else
            {
                var device = _plugin.Devices.FirstOrDefault(d => d.Id == actionParameter);
                deviceName = device?.Name ?? "Unknown";
            }

            using (var builder = new BitmapBuilder(imageSize))
            {
                builder.Clear(BitmapColor.Black);
                builder.DrawText(deviceName, BitmapColor.White, 12);
                builder.DrawText(brightness, BitmapColor.White, 40);

                return builder.ToImage();
            }
        }
    }
}
