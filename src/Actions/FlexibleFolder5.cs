using System;
using System.Collections.Generic;
using System.Linq;
using Loupedeck;
using ShellyLoupedeckPlugin.Models;

namespace ShellyLoupedeckPlugin.Actions
{
    public class FlexibleFolder5 : PluginDynamicFolder
    {
        private ShellyLoupedeckPlugin _plugin;
        private const int SLOT_INDEX = 4;

        // Navigation stack to track current path
        private Stack<string> _navigationStack = new Stack<string>();
        private string _currentLevelId = null;

        public FlexibleFolder5()
        {
            DisplayName = "Flexible Folder 5";
        }

        public override bool Load()
        {
            _plugin = (ShellyLoupedeckPlugin)Plugin;
            _plugin.FlexibleFoldersUpdated += OnFlexibleFoldersUpdated;
            UpdateDisplayName();
            return true;
        }

        public override bool Unload()
        {
            _plugin.FlexibleFoldersUpdated -= OnFlexibleFoldersUpdated;
            return true;
        }

        public override bool Activate()
        {
            // Reset to root level when folder is opened
            _navigationStack.Clear();
            _currentLevelId = null;
            return base.Activate();
        }

        private void OnFlexibleFoldersUpdated(object sender, EventArgs e)
        {
            UpdateDisplayName();
            try
            {
                ButtonActionNamesChanged();
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[FlexibleFolder5] OnFlexibleFoldersUpdated ButtonActionNamesChanged failed: {ex.Message}");
            }
        }

        private void UpdateDisplayName()
        {
            var folder = GetAssignedFolder();
            if (folder != null)
                DisplayName = folder.Name;
            else
                DisplayName = "Flexible Folder 5 (Empty)";
        }

        private FlexibleFolderConfiguration GetAssignedFolder()
        {
            if (_plugin.FlexibleFolders.Count > SLOT_INDEX)
                return _plugin.FlexibleFolders[SLOT_INDEX];
            return null;
        }

        private FlexibleFolderLevel GetCurrentLevel()
        {
            var folder = GetAssignedFolder();
            if (folder == null) return null;

            if (_currentLevelId == null)
                return folder.RootLevel;

            return FindLevelById(folder.RootLevel, _currentLevelId);
        }

        private FlexibleFolderLevel FindLevelById(FlexibleFolderLevel level, string levelId)
        {
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

        public override IEnumerable<string> GetButtonPressActionNames()
        {
            var actions = new List<string>();

            // Add back button
            if (_currentLevelId != null)
            {
                actions.Add(CreateCommandName("back"));
            }
            else
            {
                actions.Add(PluginDynamicFolder.NavigateUpActionName);
            }

            var currentLevel = GetCurrentLevel();
            if (currentLevel == null)
                return actions;

            // Debug: Log current level buttons
            DebugLogger.Log($"[FlexibleFolder5] Current level '{currentLevel.Name}' has {currentLevel.Buttons.Count} buttons");
            for (int i = 0; i < currentLevel.Buttons.Count; i++)
            {
                var btn = currentLevel.Buttons[i];
                DebugLogger.Log($"  Button {i}: Type={btn.Type}, Label='{btn.Label}', DeviceId={btn.DeviceId}, ActionType={btn.ActionType}");
            }

            // Add all buttons from current level
            for (int i = 0; i < currentLevel.Buttons.Count && i < 8; i++)
            {
                actions.Add(CreateCommandName($"button_{i}"));
            }

            return actions;
        }

        public override string GetCommandDisplayName(string actionParameter, PluginImageSize imageSize)
        {
            if (actionParameter == PluginDynamicFolder.NavigateUpActionName)
                return "Exit";

            var cmdParts = actionParameter.Split('_');
            if (cmdParts[0] == "back")
                return "Back";

            if (cmdParts[0] == "button" && cmdParts.Length >= 2)
            {
                if (int.TryParse(cmdParts[1], out int index))
                {
                    var currentLevel = GetCurrentLevel();
                    if (currentLevel != null && index < currentLevel.Buttons.Count)
                    {
                        var button = currentLevel.Buttons[index];

                        // Only show label if it's set, never auto-add device/group names
                        if (!string.IsNullOrEmpty(button.Label))
                            return button.Label;

                        // No label set - return empty or minimal placeholder
                        return "";
                    }
                }
            }

            return actionParameter;
        }

        public override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var builder = new BitmapBuilder(imageSize))
            {
                builder.Clear(BitmapColor.Black);

                var cmdParts = actionParameter.Split('_');

                // Back button
                if (actionParameter == PluginDynamicFolder.NavigateUpActionName || cmdParts[0] == "back")
                {
                    builder.DrawText("←", BitmapColor.White, 40);
                    return builder.ToImage();
                }

                if (cmdParts[0] == "button" && cmdParts.Length >= 2)
                {
                    if (int.TryParse(cmdParts[1], out int index))
                    {
                        var currentLevel = GetCurrentLevel();
                        if (currentLevel != null && index < currentLevel.Buttons.Count)
                        {
                            var button = currentLevel.Buttons[index];

                            if (button.Type == FlexibleButtonType.Navigation)
                            {
                                // Navigation button - purple/blue
                                builder.Clear(new BitmapColor(100, 50, 200));
                                builder.DrawText(button.Label ?? "→", BitmapColor.White, 18);
                                return builder.ToImage();
                            }
                            else if (button.Type == FlexibleButtonType.Action)
                            {
                                // Action button - show state-based color
                                bool isOn = false;
                                if (!string.IsNullOrEmpty(button.DeviceId))
                                {
                                    var device = _plugin.Devices.FirstOrDefault(d => d.Id == button.DeviceId);
                                    isOn = device != null && GetDeviceState(device);
                                }

                                var bgColor = isOn ? new BitmapColor(0, 150, 0) : new BitmapColor(60, 60, 60);
                                builder.Clear(bgColor);

                                // Draw label on top, device name on bottom
                                if (!string.IsNullOrEmpty(button.Label))
                                {
                                    builder.DrawText(button.Label, BitmapColor.White, 14);
                                }

                                return builder.ToImage();
                            }

                            return builder.ToImage();
                        }
                    }
                }

                builder.DrawText("?", BitmapColor.White);
                return builder.ToImage();
            }
        }

        public override void RunCommand(string actionParameter)
        {
            if (actionParameter == PluginDynamicFolder.NavigateUpActionName)
                return;

            var cmdParts = actionParameter.Split('_');

            // Back button
            if (cmdParts[0] == "back")
            {
                if (_navigationStack.Count > 0)
                {
                    _currentLevelId = _navigationStack.Pop();
                    ButtonActionNamesChanged();
                }
                return;
            }

            // Button press
            if (cmdParts[0] == "button" && cmdParts.Length >= 2)
            {
                if (int.TryParse(cmdParts[1], out int index))
                {
                    var currentLevel = GetCurrentLevel();
                    if (currentLevel != null && index < currentLevel.Buttons.Count)
                    {
                        var button = currentLevel.Buttons[index];

                        if (button.Type == FlexibleButtonType.Navigation)
                        {
                            // Navigate to target level
                            if (button.TargetLevel != null)
                            {
                                _navigationStack.Push(_currentLevelId);
                                _currentLevelId = button.TargetLevel.Id;
                                ButtonActionNamesChanged();
                            }
                        }
                        else if (button.Type == FlexibleButtonType.Action)
                        {
                            // Execute action
                            ExecuteAction(button);
                        }
                    }
                }
            }
        }

        private void ExecuteAction(FlexibleButton button)
        {
            _plugin.RecordUserAction();

            switch (button.ActionType)
            {
                case "DeviceToggle":
                    if (!string.IsNullOrEmpty(button.DeviceId))
                        _ = ToggleDeviceAsync(button.DeviceId);
                    break;

                case "GroupToggle":
                    if (!string.IsNullOrEmpty(button.GroupId))
                    {
                        var group = _plugin.Groups.FirstOrDefault(g => g.Id == button.GroupId);
                        if (group != null)
                            _ = ToggleGroupAsync(group);
                    }
                    break;

                case "Brightness":
                case "Dimmer":
                case "Color":
                case "Temperature":
                    // These would need submenu navigation - not implementing in simple version
                    DebugLogger.Log($"[FlexibleFolder5] Action {button.ActionType} requires submenu - not yet implemented");
                    break;

                default:
                    DebugLogger.Log($"[FlexibleFolder5] Unknown action type: {button.ActionType}");
                    break;
            }
        }

        private async System.Threading.Tasks.Task ToggleDeviceAsync(string deviceId)
        {
            var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
            if (device == null) return;

            bool currentState = GetDeviceState(device);
            await _plugin.ApiClient.SetRelayStateAsync(deviceId, 0, !currentState);

            await System.Threading.Tasks.Task.Delay(500);
            var updatedDevice = await _plugin.ApiClient.GetDeviceStatusAsync(deviceId);
            if (updatedDevice != null)
            {
                var index = _plugin.Devices.FindIndex(d => d.Id == deviceId);
                if (index >= 0)
                {
                    _plugin.Devices[index] = updatedDevice;
                }
            }

            ButtonActionNamesChanged();
        }

        private async System.Threading.Tasks.Task ToggleGroupAsync(DeviceGroup group)
        {
            // Check if any device is on
            bool anyOn = false;
            foreach (var deviceId in group.DeviceIds)
            {
                var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
                if (device != null && GetDeviceState(device))
                {
                    anyOn = true;
                    break;
                }
            }

            // Set all devices to opposite state
            bool targetState = !anyOn;
            foreach (var deviceId in group.DeviceIds)
            {
                await _plugin.ApiClient.SetRelayStateAsync(deviceId, 0, targetState);
                await System.Threading.Tasks.Task.Delay(300);
            }

            ButtonActionNamesChanged();
        }

        private bool GetDeviceState(ShellyDevice device, int channel = 0)
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
    }
}
