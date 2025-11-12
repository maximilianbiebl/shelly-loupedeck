using Loupedeck;
using ShellyLoupedeckPlugin.Models;

namespace ShellyLoupedeckPlugin.Actions;

public class ThermostatAdjustment : PluginDynamicAdjustment
{
    private ShellyLoupedeckPlugin _plugin;
    private Dictionary<string, double> _currentTemperature = new Dictionary<string, double>();

    public ThermostatAdjustment()
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
        return base.OnUnload();
    }

    private void OnDevicesUpdated(object? sender, EventArgs e)
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
            if (device.GetDeviceType() == DeviceType.Thermostat)
            {
                AddParameter(device.Id, device.Name, "Devices");
            }
        }

        // Add Thermostat groups
        foreach (var group in _plugin.Groups)
        {
            if (group.Type == DeviceType.Thermostat)
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
            if (device.GetDeviceType() == DeviceType.Thermostat &&
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

    protected override async void ApplyAdjustment(string actionParameter, int diff)
    {
        if (string.IsNullOrEmpty(actionParameter))
            return;

        if (actionParameter.StartsWith("group_"))
        {
            var groupId = actionParameter.Substring(6);
            var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);
            if (group != null)
            {
                foreach (var deviceId in group.DeviceIds)
                {
                    await AdjustDeviceTemperatureAsync(deviceId, diff);
                }
            }
        }
        else
        {
            await AdjustDeviceTemperatureAsync(actionParameter, diff);
        }

        AdjustmentValueChanged(actionParameter);
    }

    private async Task AdjustDeviceTemperatureAsync(string deviceId, int diff)
    {
        if (!_currentTemperature.ContainsKey(deviceId))
        {
            var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
            if (device?.Status?.Thermostats != null && device.Status.Thermostats.Count > 0)
            {
                _currentTemperature[deviceId] = device.Status.Thermostats[0].TargetTemperature?.Value ?? 20.0;
            }
            else
            {
                _currentTemperature[deviceId] = 20.0;
            }
        }

        var newTemperature = Math.Clamp(_currentTemperature[deviceId] + (diff * 0.5), 5.0, 30.0);
        _currentTemperature[deviceId] = newTemperature;

        await _plugin.ApiClient.SetThermostatTemperatureAsync(deviceId, newTemperature);
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

    protected override BitmapImage? GetCommandImage(string actionParameter, PluginImageSize imageSize)
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

        using var builder = new BitmapBuilder(imageSize);
        builder.Clear(BitmapColor.Black);
        builder.DrawText(deviceName, BitmapColor.White, 12);
        builder.DrawText(temperature, BitmapColor.White, imageSize.Height / 2 + 10, null, 20);

        return builder.ToImage();
    }
}
