using System;
using System.Linq;
using System.Threading.Tasks;
using Loupedeck;
using ShellyLoupedeckPlugin.Models;

namespace ShellyLoupedeckPlugin.Actions
{
    public class ThermostatBoostAction : PluginDynamicCommand
    {
        private ShellyLoupedeckPlugin _plugin;

        public ThermostatBoostAction() : base()
        {
            DisplayName = "Thermostat Boost";
            Description = "Activate boost mode on thermostats";
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
            return base.OnUnload();
        }

        private void OnDevicesUpdated(object sender, EventArgs e)
        {
            CreateParameters();
        }

        private void CreateParameters()
        {
            DebugLogger.Log($"=== ThermostatBoostAction: CreateParameters called, Plugin has {_plugin.Devices.Count} devices ===");

            RemoveAllParameters();

            int paramCount = 0;

            // Add individual Thermostat devices with different boost durations
            foreach (var device in _plugin.Devices)
            {
                var deviceType = device.GetDeviceType();
                if (deviceType == ShellyDeviceType.Thermostat)
                {
                    DebugLogger.Log($"  Device {device.Id} ({device.Name}): Type=Thermostat, adding boost parameters");
                    AddParameter($"{device.Id}_30", $"{device.Name} - 30min", device.Name);
                    AddParameter($"{device.Id}_60", $"{device.Name} - 60min", device.Name);
                    AddParameter($"{device.Id}_120", $"{device.Name} - 120min", device.Name);
                    paramCount += 3;
                }
            }

            // Add Thermostat groups
            foreach (var group in _plugin.Groups)
            {
                if (group.Type == ShellyDeviceType.Thermostat)
                {
                    DebugLogger.Log($"  Group {group.Id} ({group.Name}): Type=Thermostat, adding boost parameters");
                    AddParameter($"group_{group.Id}_30", $"[Group] {group.Name} - 30min", group.Name);
                    AddParameter($"group_{group.Id}_60", $"[Group] {group.Name} - 60min", group.Name);
                    AddParameter($"group_{group.Id}_120", $"[Group] {group.Name} - 120min", group.Name);
                    paramCount += 3;
                }
            }

            DebugLogger.Log($"ThermostatBoostAction: Added {paramCount} boost parameters");
            ActionImageChanged();
        }

        protected override async void RunCommand(string actionParameter)
        {
            DebugLogger.Log($"=== ThermostatBoostAction: RunCommand called with parameter: {actionParameter} ===");

            if (string.IsNullOrEmpty(actionParameter))
            {
                DebugLogger.Log("  -> Parameter is null or empty, returning");
                return;
            }

            var parts = actionParameter.Split('_');
            DebugLogger.Log($"  -> Split into {parts.Length} parts: {string.Join(", ", parts)}");

            if (parts.Length < 2)
            {
                DebugLogger.Log("  -> Not enough parts, returning");
                return;
            }

            var minutes = int.Parse(parts[parts.Length - 1]); // Last part is the duration
            DebugLogger.Log($"  -> Parsed boost duration: {minutes} minutes");

            if (actionParameter.StartsWith("group_"))
            {
                // Format: group_{groupId}_{minutes}
                var groupId = string.Join("_", parts.Skip(1).Take(parts.Length - 2).ToArray());
                DebugLogger.Log($"  -> Group action for group ID: {groupId}");
                var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);
                if (group != null)
                {
                    DebugLogger.Log($"  -> Found group with {group.DeviceIds.Count} devices");
                    foreach (var deviceId in group.DeviceIds)
                    {
                        DebugLogger.Log($"    -> Setting boost for device: {deviceId}");
                        var success = await _plugin.ApiClient.SetThermostatBoostAsync(deviceId, minutes);
                        DebugLogger.Log($"    -> Boost result: {success}");
                    }
                }
                else
                {
                    DebugLogger.Log($"  -> Group not found!");
                }
            }
            else
            {
                // Format: {deviceId}_{minutes}
                var deviceId = string.Join("_", parts.Take(parts.Length - 1).ToArray());
                DebugLogger.Log($"  -> Device action for device ID: {deviceId}");
                var success = await _plugin.ApiClient.SetThermostatBoostAsync(deviceId, minutes);
                DebugLogger.Log($"  -> Boost result: {success}");
            }
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            if (string.IsNullOrEmpty(actionParameter))
            {
                using (var bitmapBuilder = new BitmapBuilder(imageSize))
                {
                    bitmapBuilder.Clear(BitmapColor.Black);
                    bitmapBuilder.DrawText("Boost");
                    return bitmapBuilder.ToImage();
                }
            }

            var parts = actionParameter.Split('_');
            var minutes = parts[parts.Length - 1];

            using (var builder = new BitmapBuilder(imageSize))
            {
                builder.Clear(new BitmapColor(255, 100, 0)); // Orange for boost
                builder.DrawText($"Boost\n{minutes}min", BitmapColor.White);

                return builder.ToImage();
            }
        }
    }
}
