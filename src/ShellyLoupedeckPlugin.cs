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

        public ShellyApiClient ApiClient => _apiClient;
        public List<ShellyDevice> Devices => _devices;
        public List<DeviceGroup> Groups => _groups;

        public override bool UsesApplicationApiOnly => true;
        public override bool HasNoApplication => false;

        public ShellyLoupedeckPlugin()
        {
            _apiClient = new ShellyApiClient();
        }

        public override void Load()
        {
            // Load settings
            var serverUrl = GetPluginSetting("ServerUrl", "https://shelly-28-eu.shelly.cloud");
            var authKey = GetPluginSetting("AuthKey", "");

            if (!string.IsNullOrEmpty(authKey))
            {
                _apiClient.Configure(serverUrl, authKey);
                _ = RefreshDevicesAsync();
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

            // Start periodic refresh (every 30 seconds)
            _refreshTimer = new System.Threading.Timer(
                async _ => await RefreshDevicesAsync(),
                null,
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(30)
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
            SetPluginSetting("ServerUrl", serverUrl);
            SetPluginSetting("AuthKey", authKey);
            _apiClient.Configure(serverUrl, authKey);
            _ = RefreshDevicesAsync();
        }

        public async Task RefreshDevicesAsync()
        {
            if (!_apiClient.IsConfigured)
            {
                System.Diagnostics.Debug.WriteLine("Shelly Plugin: API client not configured, skipping device refresh");
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("Shelly Plugin: Starting device refresh...");
                _devices = await _apiClient.GetDevicesAsync();
                System.Diagnostics.Debug.WriteLine($"Shelly Plugin: Loaded {_devices.Count} devices");
                OnDevicesUpdated();
                System.Diagnostics.Debug.WriteLine("Shelly Plugin: Device refresh complete, parameters updated");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Shelly Plugin ERROR: {ex.GetType().Name}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Shelly Plugin ERROR: {ex.StackTrace}");

                // Show error to user
                try
                {
                    System.Windows.Forms.MessageBox.Show(
                        $"Failed to load Shelly devices:\n\n{ex.Message}\n\nPlease check:\n" +
                        "- Server URL is correct\n" +
                        "- Authorization Key is valid\n" +
                        "- You have internet connection\n" +
                        "- Shelly Cloud is reachable",
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

        protected virtual void OnDevicesUpdated()
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
