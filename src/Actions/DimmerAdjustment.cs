using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Loupedeck;
using ShellyLoupedeckPlugin.Models;

namespace ShellyLoupedeckPlugin.Actions
{
    public class DimmerAdjustment : PluginDynamicAdjustment
    {
        private ShellyLoupedeckPlugin _plugin;
        private Dictionary<string, int> _currentBrightness = new Dictionary<string, int>();
        private Dictionary<string, Timer> _debounceTimers = new Dictionary<string, Timer>();
        private readonly object _timerLock = new object();

        public DimmerAdjustment() : base(false)
        {
            DisplayName = "Dimmer Brightness";
            Description = "Adjust brightness of Shelly Dimmers";
            GroupName = "Dimmer Controls";
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

            // Add individual Dimmer devices
            foreach (var device in _plugin.Devices)
            {
                if (device.GetDeviceType() == ShellyDeviceType.Dimmer)
                {
                    AddParameter(device.Id, device.Name, "Devices");
                }
            }

            // Add Brightness groups (supports both Dimmer and RGBW)
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
                if (device.GetDeviceType() == ShellyDeviceType.Dimmer &&
                    device.Status?.Lights != null &&
                    device.Status.Lights.Count > 0)
                {
                    _currentBrightness[device.Id] = device.Status.Lights[0].Brightness;
                }
            }
        }

        protected override void ApplyAdjustment(string actionParameter, int diff)
        {
            if (string.IsNullOrEmpty(actionParameter))
                return;

            // Record user action to prevent refresh conflicts
            _plugin.RecordUserAction();

            // Update brightness value immediately for UI responsiveness
            if (actionParameter.StartsWith("group_"))
            {
                var groupId = actionParameter.Substring(6);
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
                                var device = _plugin.Devices.FirstOrDefault(d => d.Id == firstDeviceId);
                                if (device?.Status?.Lights != null && device.Status.Lights.Count > 0)
                                {
                                    _currentBrightness[firstDeviceId] = device.Status.Lights[0].Brightness;
                                }
                                else
                                {
                                    _currentBrightness[firstDeviceId] = 50;
                                }
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
            }
            else
            {
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

                _debounceTimers[actionParameter] = new Timer(async _ =>
                {
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
            if (!_currentBrightness.ContainsKey(deviceId))
            {
                var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
                if (device?.Status?.Lights != null && device.Status.Lights.Count > 0)
                {
                    _currentBrightness[deviceId] = device.Status.Lights[0].Brightness;
                }
                else
                {
                    _currentBrightness[deviceId] = 50;
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
                    DebugLogger.Log($"  -> Sending brightness update for group with {group.DeviceIds.Count} devices, calling sequentially to avoid rate limit");
                    for (int i = 0; i < group.DeviceIds.Count; i++)
                    {
                        var deviceId = group.DeviceIds[i];
                        if (_currentBrightness.ContainsKey(deviceId))
                        {
                            DebugLogger.Log($"  -> Group device {i+1}/{group.DeviceIds.Count}: {deviceId}");
                            await _plugin.ApiClient.SetLightBrightnessAsync(deviceId, _currentBrightness[deviceId]);

                            // Add 1.5 second delay between devices to respect rate limit (except after last device)
                            if (i < group.DeviceIds.Count - 1)
                            {
                                DebugLogger.Log($"  -> Waiting 1500ms before next device (rate limit prevention)");
                                await Task.Delay(1500);
                            }
                        }
                    }
                }
            }
            else
            {
                if (_currentBrightness.ContainsKey(actionParameter))
                {
                    await _plugin.ApiClient.SetLightBrightnessAsync(actionParameter, _currentBrightness[actionParameter]);
                }
            }
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
                deviceName = "Dimmer";
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
