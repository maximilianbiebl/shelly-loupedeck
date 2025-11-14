using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Loupedeck;
using ShellyLoupedeckPlugin.Models;

namespace ShellyLoupedeckPlugin.Actions
{
    public class ThermostatAdjustment : PluginDynamicAdjustment
    {
        private ShellyLoupedeckPlugin _plugin;
        private Dictionary<string, double> _currentTemperature = new Dictionary<string, double>();
        private Dictionary<string, Timer> _debounceTimers = new Dictionary<string, Timer>();
        private readonly object _timerLock = new object();

        public ThermostatAdjustment() : base(false)
        {
            DisplayName = "Thermostat Temperature";
            Description = "Adjust target temperature of thermostats";
            GroupName = "Thermostat Controls";
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
            UpdateTemperatureValues();
        }

        private void CreateParameters()
        {
            RemoveAllParameters();

            // Add individual Thermostat devices
            foreach (var device in _plugin.Devices)
            {
                if (device.GetDeviceType() == ShellyDeviceType.Thermostat)
                {
                    AddParameter(device.Id, device.Name, "Devices");
                }
            }

            // Add Thermostat groups
            foreach (var group in _plugin.Groups)
            {
                if (group.Purpose == GroupPurpose.Thermostat)
                {
                    AddParameter($"group_{group.Id}", $"[Group] {group.Name}", "Groups");
                }
            }

            AdjustmentValueChanged();
        }

        private void UpdateTemperatureValues()
        {
            foreach (var device in _plugin.Devices)
            {
                if (device.GetDeviceType() == ShellyDeviceType.Thermostat &&
                    device.Status?.Thermostats != null &&
                    device.Status.Thermostats.Count > 0)
                {
                    var thermostat = device.Status.Thermostats[0];
                    if (thermostat.TargetTemperature != null)
                    {
                        _currentTemperature[device.Id] = thermostat.TargetTemperature.Value;
                    }
                }
            }
        }

        protected override void ApplyAdjustment(string actionParameter, int diff)
        {
            DebugLogger.Log($"=== ThermostatAdjustment: ApplyAdjustment called with parameter: {actionParameter}, diff: {diff} ===");

            if (string.IsNullOrEmpty(actionParameter))
            {
                DebugLogger.Log("  -> Parameter is null or empty, returning");
                return;
            }

            // Record user action to prevent refresh conflicts
            _plugin.RecordUserAction();

            // Update temperature value immediately for UI responsiveness
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
                        UpdateTemperatureValue(deviceId, diff);
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
                UpdateTemperatureValue(actionParameter, diff);
            }

            AdjustmentValueChanged(actionParameter);

            // Debounce the API call
            lock (_timerLock)
            {
                if (_debounceTimers.ContainsKey(actionParameter))
                {
                    _debounceTimers[actionParameter]?.Dispose();
                }

                DebugLogger.Log($"  -> Starting debounce timer (200ms) for parameter: {actionParameter}");
                _debounceTimers[actionParameter] = new Timer(async _ =>
                {
                    DebugLogger.Log($"  -> Debounce timer elapsed, sending API call for parameter: {actionParameter}");
                    await SendTemperatureUpdateAsync(actionParameter);

                    lock (_timerLock)
                    {
                        if (_debounceTimers.ContainsKey(actionParameter))
                        {
                            _debounceTimers[actionParameter]?.Dispose();
                            _debounceTimers.Remove(actionParameter);
                        }
                    }
                }, null, 200, Timeout.Infinite);
            }
        }

        private void UpdateTemperatureValue(string deviceId, int diff)
        {
            if (!_currentTemperature.ContainsKey(deviceId))
            {
                var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
                if (device?.Status?.Thermostats != null && device.Status.Thermostats.Count > 0)
                {
                    _currentTemperature[deviceId] = device.Status.Thermostats[0].TargetTemperature?.Value ?? 20.0;
                    DebugLogger.Log($"    -> Initialized temperature from device status: {_currentTemperature[deviceId]}°C");
                }
                else
                {
                    _currentTemperature[deviceId] = 20.0;
                    DebugLogger.Log($"    -> Device status not available, defaulting to 20.0°C");
                }
            }

            var oldTemperature = _currentTemperature[deviceId];
            var newTemperature = Math.Max(5.0, Math.Min(30.0, _currentTemperature[deviceId] + (diff * 0.5)));
            _currentTemperature[deviceId] = newTemperature;

            DebugLogger.Log($"    -> Temperature: {oldTemperature}°C -> {newTemperature}°C (diff={diff}, step=0.5)");
        }

        private async Task SendTemperatureUpdateAsync(string actionParameter)
        {
            if (actionParameter.StartsWith("group_"))
            {
                var groupId = actionParameter.Substring(6);
                var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);
                if (group != null)
                {
                    DebugLogger.Log($"  -> Sending temperature update for group with {group.DeviceIds.Count} devices");
                    foreach (var deviceId in group.DeviceIds)
                    {
                        if (_currentTemperature.ContainsKey(deviceId))
                        {
                            DebugLogger.Log($"    -> Setting temperature for device {deviceId} to {_currentTemperature[deviceId]}°C");
                            await _plugin.ApiClient.SetThermostatTemperatureAsync(deviceId, _currentTemperature[deviceId]);
                        }
                    }
                }
            }
            else
            {
                if (_currentTemperature.ContainsKey(actionParameter))
                {
                    DebugLogger.Log($"  -> Setting temperature for device {actionParameter} to {_currentTemperature[actionParameter]}°C");
                    await _plugin.ApiClient.SetThermostatTemperatureAsync(actionParameter, _currentTemperature[actionParameter]);
                }
            }
        }

        protected override string GetAdjustmentValue(string actionParameter)
        {
            if (string.IsNullOrEmpty(actionParameter))
                return "20°C";

            double temperature = 20.0;

            if (actionParameter.StartsWith("group_"))
            {
                var groupId = actionParameter.Substring(6);
                var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);
                if (group != null && group.DeviceIds.Count > 0)
                {
                    var firstDeviceId = group.DeviceIds[0];
                    if (_currentTemperature.ContainsKey(firstDeviceId))
                    {
                        temperature = _currentTemperature[firstDeviceId];
                    }
                }
            }
            else if (_currentTemperature.ContainsKey(actionParameter))
            {
                temperature = _currentTemperature[actionParameter];
            }

            return $"{temperature:F1}°C";
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            var temperature = GetAdjustmentValue(actionParameter);

            string deviceName;
            if (string.IsNullOrEmpty(actionParameter))
            {
                deviceName = "Thermostat";
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
                builder.DrawText(temperature, BitmapColor.White, 40);

                return builder.ToImage();
            }
        }
    }
}
