using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Loupedeck;
using Newtonsoft.Json;
using ShellyLoupedeckPlugin.Api;
using ShellyLoupedeckPlugin.Models;

namespace ShellyLoupedeckPlugin
{
    public class ShellyLoupedeckPlugin : Plugin
    {
        private ShellyApiClient _apiClient;
        private List<ShellyDevice> _devices = new List<ShellyDevice>();
        private List<DeviceGroup> _groups = new List<DeviceGroup>();
        private System.Threading.Timer _refreshTimer;
        private DateTime _lastUserActionTime = DateTime.MinValue;

        // Shared color state for RGBW devices (deviceId -> (R, G, B, W, Temperature))
        public Dictionary<string, (int R, int G, int B, int W, int? Temperature)> DeviceColorStates { get; } = new Dictionary<string, (int, int, int, int, int?)>();

        // Shared brightness state for RGBW devices (updated when color is set)
        public Dictionary<string, int> DeviceBrightnessCache { get; } = new Dictionary<string, int>();

        public ShellyApiClient ApiClient => _apiClient;
        public List<ShellyDevice> Devices => _devices;
        public List<DeviceGroup> Groups => _groups;

        // Update last user action time to prevent refresh conflicts
        public void RecordUserAction()
        {
            _lastUserActionTime = DateTime.Now;
        }

        public override bool UsesApplicationApiOnly => true;
        public override bool HasNoApplication => false;

        public ShellyLoupedeckPlugin()
        {
            _apiClient = new ShellyApiClient();
        }

        public override void Load()
        {
            DebugLogger.Clear(); // Clear previous log
            DebugLogger.Log("=== Plugin Load called ===");

            // Load settings
            var serverUrl = GetPluginSetting("ServerUrl", "https://shelly-28-eu.shelly.cloud");
            var authKey = GetPluginSetting("AuthKey", "");

            DebugLogger.Log($"Loaded ServerUrl: {serverUrl}");
            DebugLogger.Log($"Loaded AuthKey length: {authKey?.Length ?? 0}");

            if (!string.IsNullOrEmpty(authKey))
            {
                DebugLogger.Log("AuthKey found, configuring API client and loading devices");
                _apiClient.Configure(serverUrl, authKey);
                _ = RefreshDevicesAsync();
            }
            else
            {
                DebugLogger.Log("No AuthKey found, skipping initial device load");
            }

            // Load groups
            var groupsJson = GetPluginSetting("Groups", "[]");
            try
            {
                _groups = JsonConvert.DeserializeObject<List<DeviceGroup>>(groupsJson) ?? new List<DeviceGroup>();
            }
            catch
            {
                _groups = new List<DeviceGroup>();
            }

            // Start periodic refresh (every 5 seconds)
            _refreshTimer = new System.Threading.Timer(
                async _ => await RefreshDevicesAsync(),
                null,
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(5)
            );

            base.Load();
        }

        public override void Unload()
        {
            if (_refreshTimer != null)
            {
                _refreshTimer.Dispose();
            }
            base.Unload();
        }

        public void SaveConfiguration(string serverUrl, string authKey)
        {
            DebugLogger.Log("=== SaveConfiguration called ===");
            DebugLogger.Log($"ServerUrl: {serverUrl}");
            DebugLogger.Log($"AuthKey length: {authKey?.Length ?? 0}");

            SetPluginSetting("ServerUrl", serverUrl);
            SetPluginSetting("AuthKey", authKey);
            _apiClient.Configure(serverUrl, authKey);

            DebugLogger.Log("Calling RefreshDevicesAsync...");
            _ = RefreshDevicesAsync();
        }

        public async Task RefreshDevicesAsync()
        {
            DebugLogger.Log("=== RefreshDevicesAsync called ===");

            if (!_apiClient.IsConfigured)
            {
                DebugLogger.Log("Shelly Plugin: API client not configured, skipping device refresh");
                return;
            }

            // Skip refresh if user was active within last 2 seconds (prevents rate limit conflicts)
            var timeSinceLastAction = DateTime.Now - _lastUserActionTime;
            if (timeSinceLastAction.TotalSeconds < 2)
            {
                DebugLogger.Log($"Shelly Plugin: Skipping refresh (user active {timeSinceLastAction.TotalMilliseconds:F0}ms ago, prevents rate limit)");
                return;
            }

            try
            {
                DebugLogger.Log("Shelly Plugin: Starting device refresh...");
                _devices = await _apiClient.GetDevicesAsync();
                DebugLogger.Log($"Shelly Plugin: Loaded {_devices.Count} devices");
                OnDevicesUpdated();
                DebugLogger.Log("Shelly Plugin: Device refresh complete, parameters updated");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"Shelly Plugin ERROR: {ex.GetType().Name}: {ex.Message}");
                DebugLogger.Log($"Shelly Plugin ERROR: {ex.StackTrace}");

                // Show error to user
                try
                {
                    System.Windows.Forms.MessageBox.Show(
                        $"Failed to load Shelly devices:\n\n{ex.Message}\n\nPlease check:\n" +
                        "- Server URL is correct\n" +
                        "- Authorization Key is valid\n" +
                        "- You have internet connection\n" +
                        "- Shelly Cloud is reachable\n\n" +
                        "Log file: %LocalAppData%\\Loupedeck\\Logs\\ShellyPlugin_Debug.log",
                        "Shelly API Error",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Error
                    );
                }
                catch
                {
                    // Ignore if message box fails
                }
            }
        }

        public void SaveGroups()
        {
            var groupsJson = JsonConvert.SerializeObject(_groups);
            SetPluginSetting("Groups", groupsJson);
        }

        public void AddGroup(DeviceGroup group)
        {
            _groups.Add(group);
            SaveGroups();
            OnDevicesUpdated();
        }

        public void RemoveGroup(string groupId)
        {
            _groups.RemoveAll(g => g.Id == groupId);
            SaveGroups();
            OnDevicesUpdated();
        }

        public void UpdateGroup(DeviceGroup group)
        {
            var index = _groups.FindIndex(g => g.Id == group.Id);
            if (index >= 0)
            {
                _groups[index] = group;
                SaveGroups();
                OnDevicesUpdated();
            }
        }

        public event EventHandler DevicesUpdated;

        public virtual void OnDevicesUpdated()
        {
            if (DevicesUpdated != null)
            {
                DevicesUpdated.Invoke(this, EventArgs.Empty);
            }
        }

        private string GetPluginSetting(string key, string defaultValue)
        {
            if (TryGetPluginSetting(key, out var value))
            {
                return value;
            }
            return defaultValue;
        }
    }
}
