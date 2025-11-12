using Loupedeck;
using ShellyLoupedeckPlugin.Models;

namespace ShellyLoupedeckPlugin.Actions;

public class DimmerAdjustment : PluginDynamicAdjustment
{
    private ShellyLoupedeckPlugin _plugin;
    private Dictionary<string, int> _currentBrightness = new Dictionary<string, int>();

    public DimmerAdjustment()
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

    protected override void OnUnload()
    {
        _plugin.DevicesUpdated -= OnDevicesUpdated;
        base.OnUnload();
    }

    private void OnDevicesUpdated(object? sender, EventArgs e)
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
            if (device.GetDeviceType() == DeviceType.Dimmer)
            {
                AddParameter(device.Id, device.Name, "Devices");
            }
        }

        // Add Dimmer groups
        foreach (var group in _plugin.Groups)
        {
            if (group.Type == DeviceType.Dimmer)
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
            if (device.GetDeviceType() == DeviceType.Dimmer &&
                device.Status?.Lights != null &&
                device.Status.Lights.Count > 0)
            {
                _currentBrightness[device.Id] = device.Status.Lights[0].Brightness;
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
                    await AdjustDeviceBrightnessAsync(deviceId, diff);
                }
            }
        }
        else
        {
            await AdjustDeviceBrightnessAsync(actionParameter, diff);
        }

        AdjustmentValueChanged(actionParameter);
    }

    private async Task AdjustDeviceBrightnessAsync(string deviceId, int diff)
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

        var newBrightness = Math.Clamp(_currentBrightness[deviceId] + (diff * 5), 0, 100);
        _currentBrightness[deviceId] = newBrightness;

        await _plugin.ApiClient.SetLightBrightnessAsync(deviceId, newBrightness);
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

    protected override BitmapImage? GetCommandImage(string actionParameter, PluginImageSize imageSize)
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

        using var builder = new BitmapBuilder(imageSize);
        builder.Clear(BitmapColor.Black);
        builder.DrawText(deviceName, BitmapColor.White, 12);
        builder.DrawText(brightness, BitmapColor.White, imageSize.Height / 2 + 10, null, 20);

        return builder.ToImage();
    }
}
