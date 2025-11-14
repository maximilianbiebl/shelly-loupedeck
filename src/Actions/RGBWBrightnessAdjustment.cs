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
                if (device.GetDeviceType() == ShellyDeviceType.RGBW)
                {
                    // Try Status.Lights first, then Lights
                    LightStatus light = null;
                    if (device.Status?.Lights != null && device.Status.Lights.Count > 0)
                    {
                        light = device.Status.Lights[0];
                    }
                    else if (device.Lights != null && device.Lights.Count > 0)
                    {
                        light = device.Lights[0];
                    }

                    if (light != null)
                    {
                        _currentBrightness[device.Id] = light.Brightness;

                        // Read actual color state from device
                        // Check if device is in color mode (any RGB > 0) or white mode
                        bool isColorMode = light.Red > 0 || light.Green > 0 || light.Blue > 0;

                        if (!_plugin.DeviceColorStates.ContainsKey(device.Id))
                        {
                            // Initialize ONLY if not already set (first time only)
                            if (isColorMode)
                            {
                                // Store maximum RGB values (at 100% brightness) for color mode
                                // This way we can scale them based on brightness
                                double currentBrightnessFactor = light.Brightness / 100.0;
                                int maxR = currentBrightnessFactor > 0 ? (int)(light.Red / currentBrightnessFactor) : light.Red;
                                int maxG = currentBrightnessFactor > 0 ? (int)(light.Green / currentBrightnessFactor) : light.Green;
                                int maxB = currentBrightnessFactor > 0 ? (int)(light.Blue / currentBrightnessFactor) : light.Blue;

                                // Clamp values to 255
                                maxR = Math.Min(255, maxR);
                                maxG = Math.Min(255, maxG);
                                maxB = Math.Min(255, maxB);

                                _plugin.DeviceColorStates[device.Id] = (maxR, maxG, maxB, 0, null);
                                DebugLogger.Log($"  Initialized device {device.Id} color state from device: COLOR mode RGB=({maxR},{maxG},{maxB}) @ {light.Brightness}%");
                            }
                            else
                            {
                                // White mode
                                _plugin.DeviceColorStates[device.Id] = (0, 0, 0, 255, null);
                                DebugLogger.Log($"  Initialized device {device.Id} color state from device: WHITE mode");
                            }
                        }
                        // Do NOT auto-detect mode changes - only trust manually set color states
                        // This prevents issues when API calls fail (e.g., rate limit) and cache is out of sync
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

            // Record user action to prevent refresh conflicts
            _plugin.RecordUserAction();

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

                DebugLogger.Log($"  -> Starting debounce timer (400ms) for parameter: {actionParameter}");
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
                }, null, 400, Timeout.Infinite);
            }
        }

        private void UpdateBrightnessValue(string deviceId, int diff)
        {
            // Always sync with cache first (in case color was changed)
            if (_plugin.DeviceBrightnessCache.ContainsKey(deviceId))
            {
                if (!_currentBrightness.ContainsKey(deviceId) ||
                    _currentBrightness[deviceId] != _plugin.DeviceBrightnessCache[deviceId])
                {
                    _currentBrightness[deviceId] = _plugin.DeviceBrightnessCache[deviceId];
                    DebugLogger.Log($"    -> Synced brightness from cache: {_currentBrightness[deviceId]}%");
                }
            }
            else if (!_currentBrightness.ContainsKey(deviceId))
            {
                // Initialize from device status if no cache available
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

            // Also update the cache
            _plugin.DeviceBrightnessCache[deviceId] = newBrightness;

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
                    // In color mode: send full RGB values + brightness parameter
                    // The Shelly API will handle the scaling internally
                    DebugLogger.Log($"    -> Device {deviceId} in COLOR mode: Setting RGB=({colorState.R},{colorState.G},{colorState.B}) with brightness={brightness}%");
                    await _plugin.ApiClient.SetLightColorAsync(deviceId, colorState.R, colorState.G, colorState.B, 0, null, null, brightness);
                    return;
                }
                else
                {
                    // In white mode: just set brightness (temperature should persist)
                    DebugLogger.Log($"    -> Device {deviceId} in WHITE mode: Setting brightness to {brightness}%");
                    await _plugin.ApiClient.SetLightBrightnessAsync(deviceId, brightness);
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
            string deviceId = null;

            if (actionParameter.StartsWith("group_"))
            {
                var groupId = actionParameter.Substring(6);
                var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);
                if (group != null && group.DeviceIds.Count > 0)
                {
                    deviceId = group.DeviceIds[0];
                }
            }
            else
            {
                deviceId = actionParameter;
            }

            if (!string.IsNullOrEmpty(deviceId))
            {
                // Sync with cache first (in case color was changed)
                if (_plugin.DeviceBrightnessCache.ContainsKey(deviceId))
                {
                    _currentBrightness[deviceId] = _plugin.DeviceBrightnessCache[deviceId];
                }

                if (_currentBrightness.ContainsKey(deviceId))
                {
                    brightness = _currentBrightness[deviceId];
                }
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
