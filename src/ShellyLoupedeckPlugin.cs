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
        private List<FolderConfiguration> _folders = new List<FolderConfiguration>();
        private List<FlexibleFolderConfiguration> _flexibleFolders = new List<FlexibleFolderConfiguration>();
        private System.Threading.Timer _refreshTimer;
        private DateTime _lastUserActionTime = DateTime.MinValue;
        private bool _isInErrorState = false;
        private int _consecutiveErrorCount = 0;

        // Shared color state for RGBW devices (deviceId -> (R, G, B, W, Temperature))
        public Dictionary<string, (int R, int G, int B, int W, int? Temperature)> DeviceColorStates { get; } = new Dictionary<string, (int, int, int, int, int?)>();

        // Shared brightness state for RGBW devices (updated when color is set)
        public Dictionary<string, int> DeviceBrightnessCache { get; } = new Dictionary<string, int>();

        public ShellyApiClient ApiClient => _apiClient;
        public List<ShellyDevice> Devices => _devices;
        public List<DeviceGroup> Groups => _groups;
        public List<FolderConfiguration> Folders => _folders;
        public List<FlexibleFolderConfiguration> FlexibleFolders => _flexibleFolders;

        // Update last user action time to prevent refresh conflicts
        public void RecordUserAction()
        {
            _lastUserActionTime = DateTime.Now;
        }

        // This plugin talks to the Shelly cloud and has no desktop application behind
        // it. Reporting an application makes Loupedeck register it as an application
        // profile rather than a universal plugin, which is how it surfaced from 6.4
        // onwards - the actions then sit behind a usage profile instead of being
        // available on any page.
        public override bool UsesApplicationApiOnly => true;
        public override bool HasNoApplication => true;

        public ShellyLoupedeckPlugin()
        {
            _apiClient = new ShellyApiClient();
        }

        public override void Load()
        {
            DebugLogger.Clear(); // Clear previous log

            // Applied before anything else is written, so the setting covers the whole session
            var verbose = GetPluginSetting("VerboseLogging", "false") == "true";
            DebugLogger.MinimumLevel = verbose ? LogLevel.Verbose : LogLevel.Info;

            DebugLogger.Log("=== Plugin Load called ===");
            LogBuildIdentity();

            if (verbose)
                DebugLogger.Log("Detailed logging is enabled (Settings > Detailed logging)");

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

            DebugLogger.Log($"Loaded {_groups.Count} groups from settings");
            OnGroupsUpdated(); // Notify folder actions that groups are loaded

            // Load folder configurations
            var foldersJson = GetPluginSetting("Folders", "[]");
            try
            {
                _folders = JsonConvert.DeserializeObject<List<FolderConfiguration>>(foldersJson) ?? new List<FolderConfiguration>();
            }
            catch
            {
                _folders = new List<FolderConfiguration>();
            }

            DebugLogger.Log($"Loaded {_folders.Count} folder configurations from settings");
            OnFoldersUpdated();

            // Load flexible folder configurations
            var flexibleFoldersJson = GetPluginSetting("FlexibleFolders", "[]");
            try
            {
                _flexibleFolders = JsonConvert.DeserializeObject<List<FlexibleFolderConfiguration>>(flexibleFoldersJson, new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.Auto,
                    PreserveReferencesHandling = PreserveReferencesHandling.Objects
                }) ?? new List<FlexibleFolderConfiguration>();
            }
            catch
            {
                _flexibleFolders = new List<FlexibleFolderConfiguration>();
            }

            DebugLogger.Log($"Loaded {_flexibleFolders.Count} flexible folder configurations from settings");
            OnFlexibleFoldersUpdated();

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

        /// <summary>
        /// Logs which build is actually running. Loupedeck keeps the previously
        /// installed DLL when a package does not raise its version, so this is
        /// how you confirm an update really took effect.
        /// </summary>
        private void LogBuildIdentity()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                var location = assembly.Location;

                DebugLogger.Log($"Plugin build: version {version}");

                if (!string.IsNullOrEmpty(location) && System.IO.File.Exists(location))
                {
                    var builtAt = System.IO.File.GetLastWriteTime(location);
                    DebugLogger.Log($"Plugin build: DLL timestamp {builtAt:yyyy-MM-dd HH:mm:ss}");
                    DebugLogger.Log($"Plugin build: loaded from {location}");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"Plugin build: identity unavailable ({ex.Message})");
            }
        }

        /// <summary>Takes effect immediately; the stored value is reapplied on the next load.</summary>
        public void SaveVerboseLogging(bool verbose)
        {
            SetPluginSetting("VerboseLogging", verbose ? "true" : "false");
            DebugLogger.MinimumLevel = verbose ? LogLevel.Verbose : LogLevel.Info;
            DebugLogger.Log($"Detailed logging {(verbose ? "enabled" : "disabled")}");
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
            // This runs every five seconds, so its tracing stays verbose-only
            DebugLogger.Verbose("=== RefreshDevicesAsync called ===");

            if (!_apiClient.IsConfigured)
            {
                DebugLogger.Verbose("Shelly Plugin: API client not configured, skipping device refresh");
                return;
            }

            // Skip refresh if user was active within last 2 seconds (prevents rate limit conflicts)
            var timeSinceLastAction = DateTime.Now - _lastUserActionTime;
            if (timeSinceLastAction.TotalSeconds < 2)
            {
                DebugLogger.Verbose($"Shelly Plugin: Skipping refresh (user active {timeSinceLastAction.TotalMilliseconds:F0}ms ago, prevents rate limit)");
                return;
            }

            try
            {
                DebugLogger.Verbose("Shelly Plugin: Starting device refresh...");
                _devices = await _apiClient.GetDevicesAsync();
                DebugLogger.Verbose($"Shelly Plugin: Loaded {_devices.Count} devices");

                // Reset error state on successful refresh
                if (_isInErrorState)
                {
                    DebugLogger.Log("Shelly Plugin: Connection restored");
                    _isInErrorState = false;
                    _consecutiveErrorCount = 0;
                }

                OnDevicesUpdated();
                DebugLogger.Verbose("Shelly Plugin: Device refresh complete, parameters updated");
            }
            catch (Exception ex)
            {
                _consecutiveErrorCount++;

                // Only log detailed error info on first occurrence or every 10th consecutive error
                if (!_isInErrorState || _consecutiveErrorCount % 10 == 0)
                {
                    DebugLogger.Error($"{ex.GetType().Name}: {ex.Message}");
                    DebugLogger.Error($"Device refresh failed (attempt {_consecutiveErrorCount}) - check internet connection and Shelly Cloud availability");

                    if (!_isInErrorState)
                    {
                        DebugLogger.Log("Shelly Plugin: Entering error state - retrying silently until the connection returns");
                        _isInErrorState = true;
                    }
                }

                // Silent retry - no popups, no spam
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
            OnGroupsUpdated();
            OnDevicesUpdated();
        }

        public void RemoveGroup(string groupId)
        {
            _groups.RemoveAll(g => g.Id == groupId);
            SaveGroups();
            OnGroupsUpdated();
            OnDevicesUpdated();
        }

        public void UpdateGroup(DeviceGroup group)
        {
            var index = _groups.FindIndex(g => g.Id == group.Id);
            if (index >= 0)
            {
                _groups[index] = group;
                SaveGroups();
                OnGroupsUpdated();
                OnDevicesUpdated();
            }
        }

        public void SaveFolders()
        {
            var foldersJson = JsonConvert.SerializeObject(_folders);
            SetPluginSetting("Folders", foldersJson);
        }

        public void AddFolder(FolderConfiguration folder)
        {
            _folders.Add(folder);
            SaveFolders();
            OnFoldersUpdated();
        }

        public void RemoveFolder(string folderId)
        {
            _folders.RemoveAll(f => f.Id == folderId);
            SaveFolders();
            OnFoldersUpdated();
        }

        public void UpdateFolder(FolderConfiguration folder)
        {
            var index = _folders.FindIndex(f => f.Id == folder.Id);
            if (index >= 0)
            {
                _folders[index] = folder;
                SaveFolders();
                OnFoldersUpdated();
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

        public event EventHandler GroupsUpdated;

        public virtual void OnGroupsUpdated()
        {
            if (GroupsUpdated != null)
            {
                GroupsUpdated.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler FoldersUpdated;

        public virtual void OnFoldersUpdated()
        {
            if (FoldersUpdated != null)
            {
                FoldersUpdated.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler FlexibleFoldersUpdated;

        public virtual void OnFlexibleFoldersUpdated()
        {
            if (FlexibleFoldersUpdated != null)
            {
                FlexibleFoldersUpdated.Invoke(this, EventArgs.Empty);
            }
        }

        // FlexibleFolder management methods
        public void SaveFlexibleFolders()
        {
            var foldersJson = JsonConvert.SerializeObject(_flexibleFolders, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                PreserveReferencesHandling = PreserveReferencesHandling.Objects
            });
            SetPluginSetting("FlexibleFolders", foldersJson);
        }

        public void AddFlexibleFolder(FlexibleFolderConfiguration folder)
        {
            _flexibleFolders.Add(folder);
            SaveFlexibleFolders();
            OnFlexibleFoldersUpdated();
        }

        public void RemoveFlexibleFolder(string folderId)
        {
            _flexibleFolders.RemoveAll(f => f.Id == folderId);
            SaveFlexibleFolders();
            OnFlexibleFoldersUpdated();
        }

        public void UpdateFlexibleFolder(FlexibleFolderConfiguration folder)
        {
            var index = _flexibleFolders.FindIndex(f => f.Id == folder.Id);
            if (index >= 0)
            {
                _flexibleFolders[index] = folder;
                SaveFlexibleFolders();
                OnFlexibleFoldersUpdated();
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
