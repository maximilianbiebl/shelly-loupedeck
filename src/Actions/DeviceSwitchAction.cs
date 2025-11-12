using Loupedeck;
using ShellyLoupedeckPlugin.Models;

namespace ShellyLoupedeckPlugin.Actions;

public class DeviceSwitchAction : PluginDynamicCommand
{
    private ShellyLoupedeckPlugin _plugin = null!;

    public DeviceSwitchAction()
    {
        DisplayName = "Device Switch";
        Description = "Toggle Shelly devices on/off";
        GroupName = "Controls";
    }

    protected override bool OnLoad()
    {
        _plugin = (ShellyLoupedeckPlugin)Plugin;
        _plugin.DevicesUpdated += OnDevicesUpdated;

        // Initial parameter creation
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
        // Remove all existing parameters
        RemoveAllParameters();

        // Add individual devices
        foreach (var device in _plugin.Devices)
        {
            var deviceType = device.GetDeviceType();
            if (deviceType == ShellyDeviceType.Switch ||
                deviceType == ShellyDeviceType.ShellyPlus2PM ||
                deviceType == ShellyDeviceType.RGBW)
            {
                AddParameter(device.Id, device.Name, "Devices");
            }
        }

        // Add groups
        foreach (var group in _plugin.Groups)
        {
            if (group.Type == ShellyDeviceType.Switch ||
                group.Type == ShellyDeviceType.ShellyPlus2PM ||
                group.Type == ShellyDeviceType.RGBW)
            {
                AddParameter($"group_{group.Id}", $"[Group] {group.Name}", "Groups");
            }
        }

        ActionImageChanged();
    }

    protected override async void RunCommand(string actionParameter)
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
                    await ToggleDeviceAsync(deviceId);
                }
            }
        }
        else
        {
            await ToggleDeviceAsync(actionParameter);
        }

        ActionImageChanged(actionParameter);
    }

    private async Task ToggleDeviceAsync(string deviceId)
    {
        var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
        if (device == null)
            return;

        var isOn = GetDeviceState(device);
        await _plugin.ApiClient.SetRelayStateAsync(deviceId, 0, !isOn);

        // Refresh device status
        await Task.Delay(500);
        var updatedDevice = await _plugin.ApiClient.GetDeviceStatusAsync(deviceId);
        if (updatedDevice != null)
        {
            var index = _plugin.Devices.FindIndex(d => d.Id == deviceId);
            if (index >= 0)
            {
                _plugin.Devices[index] = updatedDevice;
            }
        }
    }

    private bool GetDeviceState(ShellyDevice device)
    {
        if (device.Status?.Relays != null && device.Status.Relays.Count > 0)
        {
            return device.Status.Relays[0].IsOn;
        }
        if (device.Status?.Lights != null && device.Status.Lights.Count > 0)
        {
            return device.Status.Lights[0].IsOn;
        }
        return false;
    }

    protected override BitmapImage? GetCommandImage(string actionParameter, PluginImageSize imageSize)
    {
        if (string.IsNullOrEmpty(actionParameter))
        {
            using var bitmapBuilder = new BitmapBuilder(imageSize);
            bitmapBuilder.Clear(BitmapColor.Black);
            bitmapBuilder.DrawText("Switch");
            return bitmapBuilder.ToImage();
        }

        string deviceName;
        bool isOn = false;

        if (actionParameter.StartsWith("group_"))
        {
            var groupId = actionParameter.Substring(6);
            var group = _plugin.Groups.FirstOrDefault(g => g.Id == groupId);
            deviceName = group?.Name ?? "Unknown";

            // Check if any device in group is on
            if (group != null)
            {
                foreach (var deviceId in group.DeviceIds)
                {
                    var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
                    if (device != null && GetDeviceState(device))
                    {
                        isOn = true;
                        break;
                    }
                }
            }
        }
        else
        {
            var device = _plugin.Devices.FirstOrDefault(d => d.Id == actionParameter);
            deviceName = device?.Name ?? "Unknown";
            if (device != null)
            {
                isOn = GetDeviceState(device);
            }
        }

        using var builder = new BitmapBuilder(imageSize);
        builder.Clear(isOn ? new BitmapColor(0, 200, 0) : BitmapColor.Black);
        builder.DrawText(deviceName);

        return builder.ToImage();
    }
}
