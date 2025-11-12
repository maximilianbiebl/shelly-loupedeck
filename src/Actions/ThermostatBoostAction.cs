using Loupedeck;
using ShellyLoupedeckPlugin.Models;

namespace ShellyLoupedeckPlugin.Actions;

public class ThermostatBoostAction : PluginDynamicCommand
{
    private ShellyLoupedeckPlugin _plugin = null!;

    public ThermostatBoostAction()
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

    private void OnDevicesUpdated(object? sender, EventArgs e)
    {
        CreateParameters();
    }

    private void CreateParameters()
    {
        RemoveAllParameters();

        // Add individual Thermostat devices with different boost durations
        foreach (var device in _plugin.Devices)
        {
            if (device.GetDeviceType() == ShellyDeviceType.Thermostat)
            {
                AddParameter($"{device.Id}_30", $"{device.Name} - 30min", device.Name);
                AddParameter($"{device.Id}_60", $"{device.Name} - 60min", device.Name);
                AddParameter($"{device.Id}_120", $"{device.Name} - 120min", device.Name);
            }
        }

        // Add Thermostat groups
        foreach (var group in _plugin.Groups)
        {
            if (group.Type == ShellyDeviceType.Thermostat)
            {
                AddParameter($"group_{group.Id}_30", $"[Group] {group.Name} - 30min", group.Name);
                AddParameter($"group_{group.Id}_60", $"[Group] {group.Name} - 60min", group.Name);
                AddParameter($"group_{group.Id}_120", $"[Group] {group.Name} - 120min", group.Name);
            }
        }

        ActionImageChanged();
    }

    protected override async void RunCommand(string actionParameter)
    {
        if (string.IsNullOrEmpty(actionParameter))
            return;

        var parts = actionParameter.Split('_');
        if (parts.Length < 2)
            return;

        var minutes = int.Parse(parts[^1]); // Last part is the duration

        if (actionParameter.StartsWith("group_"))
        {
            // Format: group_{groupId}_{minutes}
            var groupId = string.Join("_", parts.Skip(1).Take(parts.Length - 2));
            var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);
            if (group != null)
            {
                foreach (var deviceId in group.DeviceIds)
                {
                    await _plugin.ApiClient.SetThermostatBoostAsync(deviceId, minutes);
                }
            }
        }
        else
        {
            // Format: {deviceId}_{minutes}
            var deviceId = string.Join("_", parts.Take(parts.Length - 1));
            await _plugin.ApiClient.SetThermostatBoostAsync(deviceId, minutes);
        }
    }

    protected override BitmapImage? GetCommandImage(string actionParameter, PluginImageSize imageSize)
    {
        if (string.IsNullOrEmpty(actionParameter))
        {
            using var bitmapBuilder = new BitmapBuilder(imageSize);
            bitmapBuilder.Clear(BitmapColor.Black);
            bitmapBuilder.DrawText("Boost");
            return bitmapBuilder.ToImage();
        }

        var parts = actionParameter.Split('_');
        var minutes = parts[^1];

        using var builder = new BitmapBuilder(imageSize);
        builder.Clear(new BitmapColor(255, 100, 0)); // Orange for boost
        builder.DrawText($"Boost\n{minutes}min", BitmapColor.White);

        return builder.ToImage();
    }
}
