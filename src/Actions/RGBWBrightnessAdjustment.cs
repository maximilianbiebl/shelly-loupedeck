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
        private Dictionary<string, bool> _groupOperationInProgress = new Dictionary<string, bool>();
        private readonly object _timerLock = new object();
        private readonly object _operationLock = new object();

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

            // Add RGBW/Dimmer groups
            foreach (var group in _plugin.Groups)
            {
                if (group.Purpose == GroupPurpose.Brightness)
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
                        // Read actual color state from device
                        // Check if device is in color mode (any RGB > 0) or white mode
                        bool isColorMode = light.Red > 0 || light.Green > 0 || light.Blue > 0;

                        // In color mode, use 'gain' for brightness; in white mode, use 'brightness'
                        int currentBrightnessValue = isColorMode ? light.Gain : light.Brightness;
                        _currentBrightness[device.Id] = currentBrightnessValue;

                        if (!_plugin.DeviceColorStates.ContainsKey(device.Id))
                        {
                            // Initialize ONLY if not already set (first time only)
                            if (isColorMode)
                            {
                                // Store RGB values at current gain level
                                _plugin.DeviceColorStates[device.Id] = (light.Red, light.Green, light.Blue, 0, null);
                                DebugLogger.Log($"  Initialized device {device.Id} color state from device: COLOR mode RGB=({light.Red},{light.Green},{light.Blue}) @ gain={light.Gain}%");
                            }
                            else
                            {
                                // White mode
                                _plugin.DeviceColorStates[device.Id] = (0, 0, 0, 255, null);
                                DebugLogger.Log($"  Initialized device {device.Id} color state from device: WHITE mode @ brightness={light.Brightness}%");
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
                    DebugLogger.Log($"  -> Found group with {group.DeviceIds.Count} devices");

                    // For groups: sync all devices to same brightness value
                    // Use first device as reference and apply diff to it
                    if (group.DeviceIds.Count > 0)
                    {
                        var firstDeviceId = group.DeviceIds[0];

                        // Get current brightness of first device
                        if (!_currentBrightness.ContainsKey(firstDeviceId))
                        {
                            // Initialize from cache or default
                            if (_plugin.DeviceBrightnessCache.ContainsKey(firstDeviceId))
                            {
                                _currentBrightness[firstDeviceId] = _plugin.DeviceBrightnessCache[firstDeviceId];
                            }
                            else
                            {
                                _currentBrightness[firstDeviceId] = 50;
                            }
                        }

                        // Apply diff to first device
                        var oldBrightness = _currentBrightness[firstDeviceId];
                        var newBrightness = Math.Max(0, Math.Min(100, oldBrightness + diff));

                        DebugLogger.Log($"  -> Group brightness: {oldBrightness}% -> {newBrightness}% (diff={diff})");

                        // Set ALL devices to this new value
                        foreach (var deviceId in group.DeviceIds)
                        {
                            _currentBrightness[deviceId] = newBrightness;
                            _plugin.DeviceBrightnessCache[deviceId] = newBrightness;
                        }
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
                // Check if group operation is already in progress
                lock (_operationLock)
                {
                    if (_groupOperationInProgress.ContainsKey(actionParameter) && _groupOperationInProgress[actionParameter])
                    {
                        DebugLogger.Log($"  -> Group operation already in progress for {actionParameter}, skipping this request to prevent rate limit");
                        return;
                    }
                    _groupOperationInProgress[actionParameter] = true;
                }

                try
                {
                    var groupId = actionParameter.Substring(6);
                    var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);
                    if (group != null)
                    {
                        DebugLogger.Log($"  -> Sending brightness update for group '{group.Name}' (Purpose: {group.Purpose}) with {group.DeviceIds.Count} devices");

                        // For color groups, ensure all devices get the same color
                        (int R, int G, int B, int W, int? temp) groupColorState = (0, 0, 0, 0, null);
                        if (group.Purpose == GroupPurpose.Color || group.Purpose == GroupPurpose.Brightness)
                        {
                            // Try to get color from first device in group, or use a default
                            if (group.DeviceIds.Count > 0 && _plugin.DeviceColorStates.ContainsKey(group.DeviceIds[0]))
                            {
                                groupColorState = _plugin.DeviceColorStates[group.DeviceIds[0]];
                                DebugLogger.Log($"  -> Using color from first device: RGB=({groupColorState.R},{groupColorState.G},{groupColorState.B})");
                            }
                            else
                            {
                                // Default to warm white color
                                groupColorState = (255, 180, 100, 0, null);
                                DebugLogger.Log($"  -> Using default warm white color: RGB=({groupColorState.R},{groupColorState.G},{groupColorState.B})");
                            }
                        }

                        for (int i = 0; i < group.DeviceIds.Count; i++)
                        {
                            var deviceId = group.DeviceIds[i];
                            DebugLogger.Log($"  -> Group device {i+1}/{group.DeviceIds.Count}: {deviceId}");

                            // Record user action before each device to prevent refresh task collision
                            _plugin.RecordUserAction();

                            // For color/brightness groups, sync color state across all devices
                            if (group.Purpose == GroupPurpose.Color || group.Purpose == GroupPurpose.Brightness)
                            {
                                _plugin.DeviceColorStates[deviceId] = groupColorState;
                            }

                            await SendDeviceBrightnessAsync(deviceId);

                            // Add 2 second delay between devices to respect rate limit (except after last device)
                            if (i < group.DeviceIds.Count - 1)
                            {
                                DebugLogger.Log($"  -> Waiting 2000ms before next device (rate limit prevention)");
                                await Task.Delay(2000);
                            }
                        }
                    }
                }
                finally
                {
                    // Always reset the flag when done
                    lock (_operationLock)
                    {
                        _groupOperationInProgress[actionParameter] = false;
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
