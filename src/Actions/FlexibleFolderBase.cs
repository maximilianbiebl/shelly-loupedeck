using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Loupedeck;
using ShellyLoupedeckPlugin.Models;

namespace ShellyLoupedeckPlugin.Actions
{
    /// <summary>
    /// Shared implementation behind the ten flexible folder slots. Each slot is a
    /// thin subclass supplying only its index and label, so behaviour lives in one
    /// place instead of being duplicated ten times.
    ///
    /// Two navigation modes exist side by side:
    ///  - browsing the user-configured level tree (<see cref="_currentLevelId"/>)
    ///  - a transient adjustment view for brightness, colour or temperature
    ///    (<see cref="_actionKind"/>), which is not part of the saved configuration
    /// </summary>
    public abstract class FlexibleFolderBase : PluginDynamicFolder
    {
        /// <summary>Index into the plugin's flexible folder list.</summary>
        protected abstract int SlotIndex { get; }

        /// <summary>Label shown while no folder is assigned to this slot.</summary>
        protected abstract string SlotName { get; }

        private ShellyLoupedeckPlugin _plugin;

        // Level browsing
        private readonly Stack<string> _navigationStack = new Stack<string>();
        private string _currentLevelId;

        // Adjustment view; null while browsing levels
        private string _actionKind;
        private string _actionDeviceId;

        private System.Threading.Timer _refreshTimer;

        // Step layouts for the adjustment rows. null marks the read-only value tile.
        private static readonly string[] PercentSteps = { "-10", "-5", "-1", null, "+1", "+5", "+10" };
        private static readonly string[] TemperatureSteps = { "-2", "-1", "-0.5", null, "+0.5", "+1", "+2" };
        private static readonly string[] ColorSteps = { "-50", "-20", "-5", null, "+5", "+20", "+50" };

        public override bool Load()
        {
            _plugin = (ShellyLoupedeckPlugin)Plugin;
            _plugin.FlexibleFoldersUpdated += OnFoldersUpdated;
            _plugin.DevicesUpdated += OnDevicesUpdated;
            UpdateDisplayName();
            return true;
        }

        public override bool Unload()
        {
            _plugin.FlexibleFoldersUpdated -= OnFoldersUpdated;
            _plugin.DevicesUpdated -= OnDevicesUpdated;
            _refreshTimer?.Dispose();
            _refreshTimer = null;
            return true;
        }

        public override bool Activate()
        {
            _navigationStack.Clear();
            _currentLevelId = null;
            ClearActionView();

            // Keep displayed values current while the folder is open
            _refreshTimer?.Dispose();
            _refreshTimer = new System.Threading.Timer(
                _ => SafeRefresh(),
                null,
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(5));

            return base.Activate();
        }

        public override bool Deactivate()
        {
            _refreshTimer?.Dispose();
            _refreshTimer = null;
            return base.Deactivate();
        }

        private void OnFoldersUpdated(object sender, EventArgs e)
        {
            UpdateDisplayName();
            SafeRefresh();
        }

        private void OnDevicesUpdated(object sender, EventArgs e) => SafeRefresh();

        private void SafeRefresh()
        {
            try
            {
                ButtonActionNamesChanged();
            }
            catch (Exception ex)
            {
                DebugLogger.Warn($"[{SlotName}] Refresh failed: {ex.Message}");
            }
        }

        private void UpdateDisplayName()
        {
            var folder = GetAssignedFolder();
            DisplayName = folder != null ? folder.Name : $"{SlotName} (Empty)";
        }

        private FlexibleFolderConfiguration GetAssignedFolder()
        {
            return _plugin.FlexibleFolders.Count > SlotIndex
                ? _plugin.FlexibleFolders[SlotIndex]
                : null;
        }

        private FlexibleFolderLevel GetCurrentLevel()
        {
            var folder = GetAssignedFolder();
            if (folder == null)
                return null;

            return _currentLevelId == null
                ? folder.RootLevel
                : FindLevelById(folder.RootLevel, _currentLevelId);
        }

        private static FlexibleFolderLevel FindLevelById(FlexibleFolderLevel level, string levelId)
        {
            if (level == null)
                return null;

            if (level.Id == levelId)
                return level;

            foreach (var button in level.Buttons)
            {
                if (button.Type == FlexibleButtonType.Navigation && button.TargetLevel != null)
                {
                    var found = FindLevelById(button.TargetLevel, levelId);
                    if (found != null)
                        return found;
                }
            }

            return null;
        }

        /// <summary>
        /// Resolves the button an action parameter refers to. The parameter carries the
        /// level id rather than relying on the current navigation state, because action
        /// parameters double as the host's image cache key: an index alone repeats across
        /// levels and would make every level render the first level's buttons.
        /// </summary>
        private FlexibleButton ResolveButton(string levelId, int index)
        {
            var folder = GetAssignedFolder();
            if (folder == null)
                return null;

            var level = FindLevelById(folder.RootLevel, levelId);
            if (level == null || index < 0 || index >= level.Buttons.Count)
                return null;

            return level.Buttons[index];
        }

        public override IEnumerable<string> GetButtonPressActionNames()
        {
            var actions = new List<string>();

            // Back leaves an adjustment view, or climbs the level tree
            if (_actionKind != null || _currentLevelId != null)
                actions.Add(CreateCommandName("back"));
            else
                actions.Add(PluginDynamicFolder.NavigateUpActionName);

            if (_actionKind != null)
            {
                actions.AddRange(GetActionViewNames());
                return actions;
            }

            var currentLevel = GetCurrentLevel();
            if (currentLevel == null)
                return actions;

            for (int i = 0; i < currentLevel.Buttons.Count && i < 8; i++)
                actions.Add(CreateCommandName($"button_{currentLevel.Id}_{i}"));

            return actions;
        }

        private IEnumerable<string> GetActionViewNames()
        {
            var id = _actionDeviceId;

            switch (_actionKind)
            {
                case "color":
                    return new[]
                    {
                        CreateCommandName($"preset_white_{id}"),
                        CreateCommandName($"preset_warm_{id}"),
                        CreateCommandName($"preset_cold_{id}"),
                        CreateCommandName($"chan_r_{id}"),
                        CreateCommandName($"chan_g_{id}"),
                        CreateCommandName($"chan_b_{id}"),
                        CreateCommandName($"open_brightness_{id}")
                    };

                case "temperature":
                    return BuildStepRow(TemperatureSteps, "temperature", id);

                case "color-r":
                case "color-g":
                case "color-b":
                    return BuildStepRow(ColorSteps, _actionKind, id);

                default: // brightness, dim
                    return BuildStepRow(PercentSteps, _actionKind, id);
            }
        }

        private IEnumerable<string> BuildStepRow(string[] steps, string kind, string deviceId)
        {
            foreach (var step in steps)
            {
                yield return step == null
                    ? CreateCommandName($"val_{kind}_{deviceId}")
                    : CreateCommandName($"adj_{kind}_{deviceId}_{step}");
            }
        }

        public override string GetCommandDisplayName(string actionParameter, PluginImageSize imageSize)
        {
            if (actionParameter == PluginDynamicFolder.NavigateUpActionName)
                return "Exit";

            var parts = actionParameter.Split('_');

            switch (parts[0])
            {
                case "back":
                    return "Back";

                case "button" when parts.Length >= 3 && int.TryParse(parts[2], out var index):
                    // Only user-set labels are shown; device names are never filled in
                    return ResolveButton(parts[1], index)?.Label ?? "";

                case "adj" when parts.Length >= 4:
                    return FormatStep(parts[1], parts[3]);

                case "val" when parts.Length >= 3:
                    return FormatValue(parts[1], parts[2]);

                case "preset" when parts.Length >= 2:
                    return PresetLabel(parts[1]);

                case "chan" when parts.Length >= 2:
                    return parts[1].ToUpperInvariant();

                case "open":
                    return "Brightness";
            }

            return actionParameter;
        }

        private static string FormatStep(string kind, string step)
        {
            if (kind == "temperature")
                return $"{step}°C";
            if (kind.StartsWith("color-"))
                return step;
            return $"{step}%";
        }

        private string FormatValue(string kind, string deviceId)
        {
            var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
            if (device == null)
                return "---";

            switch (kind)
            {
                case "temperature":
                    return $"{GetDeviceTemperature(device):F1}°C";
                case "color-r":
                    return $"R: {GetDeviceColor(device).R}";
                case "color-g":
                    return $"G: {GetDeviceColor(device).G}";
                case "color-b":
                    return $"B: {GetDeviceColor(device).B}";
                default:
                    return $"{GetDeviceBrightness(device)}%";
            }
        }

        private static string PresetLabel(string preset)
        {
            switch (preset)
            {
                case "white": return "Weiß";
                case "warm": return "Warm Weiß";
                case "cold": return "Kalt Weiß";
                default: return preset;
            }
        }

        public override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var builder = new BitmapBuilder(imageSize))
            {
                builder.Clear(BitmapColor.Black);

                if (actionParameter == PluginDynamicFolder.NavigateUpActionName)
                {
                    builder.DrawText("←", BitmapColor.White, 40);
                    return builder.ToImage();
                }

                var parts = actionParameter.Split('_');

                switch (parts[0])
                {
                    case "back":
                        builder.DrawText("←", BitmapColor.White, 40);
                        return builder.ToImage();

                    case "button" when parts.Length >= 3 && int.TryParse(parts[2], out var index):
                        DrawConfiguredButton(builder, ResolveButton(parts[1], index));
                        return builder.ToImage();

                    case "adj" when parts.Length >= 4:
                        DrawStep(builder, parts[1], parts[3]);
                        return builder.ToImage();

                    case "val" when parts.Length >= 3:
                        DrawValue(builder, parts[1], parts[2]);
                        return builder.ToImage();

                    case "preset" when parts.Length >= 2:
                        DrawPreset(builder, parts[1]);
                        return builder.ToImage();

                    case "chan" when parts.Length >= 2:
                        DrawChannel(builder, parts[1]);
                        return builder.ToImage();

                    case "open":
                        builder.Clear(new BitmapColor(100, 100, 0));
                        builder.DrawText("☀", BitmapColor.White, 36);
                        return builder.ToImage();
                }

                return builder.ToImage();
            }
        }

        private void DrawConfiguredButton(BitmapBuilder builder, FlexibleButton button)
        {
            if (button == null)
                return;

            if (button.Type == FlexibleButtonType.Navigation)
            {
                builder.Clear(new BitmapColor(100, 50, 200));
                builder.DrawText(button.Label ?? "→", BitmapColor.White, 18);
                return;
            }

            var isAdjustment = button.ActionType == "Brightness"
                || button.ActionType == "Dimmer"
                || button.ActionType == "Color"
                || button.ActionType == "Temperature";

            if (isAdjustment)
            {
                // Adjustment entries open a submenu, so they show their own accent
                // colour rather than an on/off state
                BitmapColor background;
                string glyph;

                switch (button.ActionType)
                {
                    case "Color":
                        background = new BitmapColor(100, 50, 150);
                        glyph = "🎨";
                        break;
                    case "Temperature":
                        background = new BitmapColor(150, 80, 0);
                        glyph = "🌡";
                        break;
                    default:
                        background = new BitmapColor(100, 100, 0);
                        glyph = "☀";
                        break;
                }

                builder.Clear(background);

                if (!string.IsNullOrEmpty(button.Label))
                    builder.DrawText(button.Label, BitmapColor.White, 14);
                else
                    builder.DrawText(glyph, BitmapColor.White, 32);

                return;
            }

            var isOn = false;
            if (!string.IsNullOrEmpty(button.DeviceId))
            {
                var device = _plugin.Devices.FirstOrDefault(d => d.Id == button.DeviceId);
                isOn = device != null && GetDeviceState(device);
            }

            builder.Clear(isOn ? new BitmapColor(0, 150, 0) : new BitmapColor(60, 60, 60));

            if (!string.IsNullOrEmpty(button.Label))
                builder.DrawText(button.Label, BitmapColor.White, 14);
        }

        private static void DrawStep(BitmapBuilder builder, string kind, string step)
        {
            var positive = step.StartsWith("+");

            if (kind == "temperature")
                builder.Clear(positive ? new BitmapColor(150, 80, 0) : new BitmapColor(0, 80, 150));
            else if (kind == "color-r")
                builder.Clear(positive ? new BitmapColor(120, 0, 0) : new BitmapColor(60, 0, 0));
            else if (kind == "color-g")
                builder.Clear(positive ? new BitmapColor(0, 120, 0) : new BitmapColor(0, 60, 0));
            else if (kind == "color-b")
                builder.Clear(positive ? new BitmapColor(0, 0, 120) : new BitmapColor(0, 0, 60));
            else
                builder.Clear(positive ? new BitmapColor(0, 100, 0) : new BitmapColor(100, 0, 0));

            builder.DrawText(FormatStep(kind, step), BitmapColor.White, 22);
        }

        private void DrawValue(BitmapBuilder builder, string kind, string deviceId)
        {
            var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
            if (device == null)
            {
                builder.DrawText("---", BitmapColor.White, 22);
                return;
            }

            if (kind == "temperature")
            {
                builder.Clear(new BitmapColor(100, 60, 0));
                builder.DrawText($"{GetDeviceTemperature(device):F1}°C", BitmapColor.White, 22);
                return;
            }

            if (kind.StartsWith("color-"))
            {
                var color = GetDeviceColor(device);
                var channel = kind.Substring("color-".Length);
                var value = channel == "r" ? color.R : channel == "g" ? color.G : color.B;

                builder.Clear(channel == "r"
                    ? new BitmapColor((byte)value, 0, 0)
                    : channel == "g"
                        ? new BitmapColor(0, (byte)value, 0)
                        : new BitmapColor(0, 0, (byte)value));

                builder.DrawText(value.ToString(), value > 128 ? BitmapColor.Black : BitmapColor.White, 26);
                return;
            }

            var brightness = GetDeviceBrightness(device);
            var gray = (byte)(brightness * 2.55);
            builder.Clear(new BitmapColor(gray, gray, gray));
            builder.DrawText($"{brightness}%", brightness > 50 ? BitmapColor.Black : BitmapColor.White, 26);
        }

        private static void DrawPreset(BitmapBuilder builder, string preset)
        {
            switch (preset)
            {
                case "white":
                    builder.Clear(new BitmapColor(255, 255, 255));
                    builder.DrawText("W", BitmapColor.Black, 40);
                    break;
                case "warm":
                    builder.Clear(new BitmapColor(255, 200, 150));
                    builder.DrawText("WW", BitmapColor.Black, 32);
                    break;
                case "cold":
                    builder.Clear(new BitmapColor(200, 220, 255));
                    builder.DrawText("CW", BitmapColor.Black, 32);
                    break;
            }
        }

        private static void DrawChannel(BitmapBuilder builder, string channel)
        {
            switch (channel)
            {
                case "r":
                    builder.Clear(new BitmapColor(200, 0, 0));
                    builder.DrawText("R", BitmapColor.White, 40);
                    break;
                case "g":
                    builder.Clear(new BitmapColor(0, 200, 0));
                    builder.DrawText("G", BitmapColor.White, 40);
                    break;
                case "b":
                    builder.Clear(new BitmapColor(0, 0, 200));
                    builder.DrawText("B", BitmapColor.White, 40);
                    break;
            }
        }

        public override void RunCommand(string actionParameter)
        {
            if (actionParameter == PluginDynamicFolder.NavigateUpActionName)
                return;

            var parts = actionParameter.Split('_');

            switch (parts[0])
            {
                case "back":
                    NavigateBack();
                    return;

                case "button" when parts.Length >= 3 && int.TryParse(parts[2], out var index):
                    PressConfiguredButton(ResolveButton(parts[1], index));
                    return;

                case "adj" when parts.Length >= 4:
                    _plugin.RecordUserAction();
                    _ = ApplyStepAsync(parts[1], parts[2], parts[3]);
                    return;

                case "val":
                    return; // read-only tile

                case "preset" when parts.Length >= 3:
                    _plugin.RecordUserAction();
                    _ = ApplyPresetAsync(parts[1], parts[2]);
                    return;

                case "chan" when parts.Length >= 3:
                    OpenActionView($"color-{parts[1]}", parts[2]);
                    return;

                case "open" when parts.Length >= 3:
                    OpenActionView("brightness", parts[2]);
                    return;
            }
        }

        private void NavigateBack()
        {
            // Colour channel views return to the colour menu rather than the level
            if (_actionKind != null && _actionKind.StartsWith("color-"))
            {
                _actionKind = "color";
                SafeRefresh();
                return;
            }

            if (_actionKind != null)
            {
                ClearActionView();
                SafeRefresh();
                return;
            }

            if (_navigationStack.Count > 0)
            {
                _currentLevelId = _navigationStack.Pop();
                SafeRefresh();
            }
        }

        private void PressConfiguredButton(FlexibleButton button)
        {
            if (button == null)
                return;

            if (button.Type == FlexibleButtonType.Navigation)
            {
                if (button.TargetLevel == null)
                    return;

                _navigationStack.Push(_currentLevelId);
                _currentLevelId = button.TargetLevel.Id;
                SafeRefresh();
                return;
            }

            _plugin.RecordUserAction();

            switch (button.ActionType)
            {
                case "DeviceToggle":
                    if (!string.IsNullOrEmpty(button.DeviceId))
                        _ = ToggleDeviceAsync(button.DeviceId);
                    break;

                case "GroupToggle":
                    var group = _plugin.Groups.FirstOrDefault(g => g.Id == button.GroupId);
                    if (group != null)
                        _ = ToggleGroupAsync(group);
                    break;

                case "Brightness":
                    OpenActionView("brightness", button.DeviceId);
                    break;

                case "Dimmer":
                    OpenActionView("dim", button.DeviceId);
                    break;

                case "Color":
                    OpenActionView("color", button.DeviceId);
                    break;

                case "Temperature":
                    OpenActionView("temperature", button.DeviceId);
                    break;

                default:
                    DebugLogger.Warn($"[{SlotName}] Unknown action type: {button.ActionType}");
                    break;
            }
        }

        private void OpenActionView(string kind, string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId))
            {
                DebugLogger.Warn($"[{SlotName}] Cannot open {kind} view: button has no device");
                return;
            }

            _actionKind = kind;
            _actionDeviceId = deviceId;
            SafeRefresh();
        }

        private void ClearActionView()
        {
            _actionKind = null;
            _actionDeviceId = null;
        }

        private async Task ApplyStepAsync(string kind, string deviceId, string step)
        {
            var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
            if (device == null)
                return;

            if (kind == "temperature")
            {
                if (!double.TryParse(step, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var tempDelta))
                    return;

                var target = Math.Max(5.0, Math.Min(35.0, GetDeviceTemperature(device) + tempDelta));
                await _plugin.ApiClient.SetThermostatTemperatureAsync(deviceId, target);
            }
            else if (kind.StartsWith("color-"))
            {
                if (!int.TryParse(step, System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture, out var colorDelta))
                    return;

                var color = GetDeviceColor(device);
                int r = color.R, g = color.G, b = color.B;
                var channel = kind.Substring("color-".Length);

                if (channel == "r") r = Clamp(r + colorDelta, 0, 255);
                else if (channel == "g") g = Clamp(g + colorDelta, 0, 255);
                else b = Clamp(b + colorDelta, 0, 255);

                var brightness = GetDeviceBrightness(device);
                await _plugin.ApiClient.SetLightColorAsync(deviceId, r, g, b, 0,
                    brightness: brightness == 0 ? 100 : brightness);
            }
            else
            {
                if (!int.TryParse(step, System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture, out var pctDelta))
                    return;

                var target = Clamp(GetDeviceBrightness(device) + pctDelta, 0, 100);
                await _plugin.ApiClient.SetLightBrightnessAsync(deviceId, target);
            }

            await RefreshDeviceAsync(deviceId);
        }

        private async Task ApplyPresetAsync(string preset, string deviceId)
        {
            int r, g, b;
            switch (preset)
            {
                case "white": r = 255; g = 255; b = 255; break;
                case "warm": r = 255; g = 200; b = 150; break;
                case "cold": r = 200; g = 220; b = 255; break;
                default: return;
            }

            var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
            var brightness = device != null ? GetDeviceBrightness(device) : 100;

            await _plugin.ApiClient.SetLightColorAsync(deviceId, r, g, b, 0,
                brightness: brightness == 0 ? 100 : brightness);

            await RefreshDeviceAsync(deviceId);
        }

        private async Task ToggleDeviceAsync(string deviceId)
        {
            var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
            if (device == null)
                return;

            var newState = !GetDeviceState(device);

            // Lights and dimmers are switched through the light endpoint; using the
            // relay endpoint for them silently does nothing
            var type = device.GetDeviceType();
            if (type == ShellyDeviceType.RGBW || type == ShellyDeviceType.Dimmer)
                await _plugin.ApiClient.SetLightStateAsync(deviceId, 0, newState);
            else
                await _plugin.ApiClient.SetRelayStateAsync(deviceId, 0, newState);

            await RefreshDeviceAsync(deviceId);
        }

        private async Task ToggleGroupAsync(DeviceGroup group)
        {
            var anyOn = group.DeviceIds
                .Select(id => _plugin.Devices.FirstOrDefault(d => d.Id == id))
                .Any(d => d != null && GetDeviceState(d));

            var target = !anyOn;

            foreach (var deviceId in group.DeviceIds)
            {
                var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
                if (device == null)
                    continue;

                _plugin.RecordUserAction();

                var type = device.GetDeviceType();
                if (type == ShellyDeviceType.RGBW || type == ShellyDeviceType.Dimmer)
                    await _plugin.ApiClient.SetLightStateAsync(deviceId, 0, target);
                else
                    await _plugin.ApiClient.SetRelayStateAsync(deviceId, 0, target);

                await Task.Delay(300);
            }

            SafeRefresh();
        }

        private async Task RefreshDeviceAsync(string deviceId)
        {
            await Task.Delay(2000); // give the device time to report the new state

            try
            {
                var updated = await _plugin.ApiClient.GetDeviceStatusAsync(deviceId);
                if (updated != null)
                {
                    var index = _plugin.Devices.FindIndex(d => d.Id == deviceId);
                    if (index >= 0)
                        _plugin.Devices[index] = updated;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Warn($"[{SlotName}] Status refresh for {deviceId} failed: {ex.Message}");
            }

            SafeRefresh();
        }

        private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));

        private static bool GetDeviceState(ShellyDevice device, int channel = 0)
        {
            if (device.Switch0 != null && channel == 0)
                return device.Switch0.Output;

            if (device.Status?.Relays != null && device.Status.Relays.Count > channel)
                return device.Status.Relays[channel].IsOn;
            if (device.Status?.Lights != null && device.Status.Lights.Count > channel)
                return device.Status.Lights[channel].IsOn;

            if (device.Relays != null && device.Relays.Count > channel)
                return device.Relays[channel].IsOn;
            if (device.Lights != null && device.Lights.Count > channel)
                return device.Lights[channel].IsOn;

            return false;
        }

        private static int GetDeviceBrightness(ShellyDevice device, int channel = 0)
        {
            if (device.Status?.Lights != null && device.Status.Lights.Count > channel)
                return device.Status.Lights[channel].Brightness;
            if (device.Lights != null && device.Lights.Count > channel)
                return device.Lights[channel].Brightness;
            return 0;
        }

        private static double GetDeviceTemperature(ShellyDevice device)
        {
            if (device.Status?.Thermostats != null && device.Status.Thermostats.Count > 0)
                return device.Status.Thermostats[0].TargetTemperature?.Value ?? 20.0;
            if (device.Thermostats != null && device.Thermostats.Count > 0)
                return device.Thermostats[0].TargetTemperature?.Value ?? 20.0;
            return 20.0;
        }

        private static (int R, int G, int B) GetDeviceColor(ShellyDevice device, int channel = 0)
        {
            if (device.Status?.Lights != null && device.Status.Lights.Count > channel)
            {
                var light = device.Status.Lights[channel];
                return (light.Red, light.Green, light.Blue);
            }
            if (device.Lights != null && device.Lights.Count > channel)
            {
                var light = device.Lights[channel];
                return (light.Red, light.Green, light.Blue);
            }
            return (0, 0, 0);
        }
    }
}
