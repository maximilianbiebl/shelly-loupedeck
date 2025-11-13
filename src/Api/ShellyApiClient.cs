using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ShellyLoupedeckPlugin.Models;

namespace ShellyLoupedeckPlugin.Api
{
    public class ShellyApiClient
    {
        private readonly HttpClient _httpClient;
        private string _serverUrl = "https://shelly-28-eu.shelly.cloud";
        private string _authKey = string.Empty;
        private DateTime _lastRequestTime = DateTime.MinValue;
        private readonly TimeSpan _rateLimitDelay = TimeSpan.FromSeconds(1); // 1 request per second

        public ShellyApiClient()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
        }

        public void Configure(string serverUrl, string authKey)
        {
            _serverUrl = serverUrl.TrimEnd('/');
            _authKey = authKey;
        }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_authKey);

        private async Task RateLimitAsync()
        {
            var timeSinceLastRequest = DateTime.Now - _lastRequestTime;
            if (timeSinceLastRequest < _rateLimitDelay)
            {
                var delayTime = _rateLimitDelay - timeSinceLastRequest;
                await Task.Delay(delayTime);
            }
            _lastRequestTime = DateTime.Now;
        }

        public async Task<List<ShellyDevice>> GetDevicesAsync()
        {
            if (!IsConfigured)
                throw new InvalidOperationException("API client not configured");

            await RateLimitAsync();

            try
            {
                var url = $"{_serverUrl}/device/all_status?auth_key={_authKey}";
                DebugLogger.Log($"Shelly API: Requesting devices from {_serverUrl}");
                DebugLogger.Log($"Shelly API: Full URL: {url}");

                var response = await _httpClient.GetAsync(url);
                DebugLogger.Log($"Shelly API: Response status {response.StatusCode}");

                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                DebugLogger.Log($"Shelly API: Response content length: {content.Length}");
                DebugLogger.Log($"Shelly API: Raw JSON response: {content}");

                var result = JsonConvert.DeserializeObject<DeviceListResponse>(content);

                DebugLogger.Log($"Shelly API: Deserialized result - IsOk: {result?.IsOk}, Data is null: {result?.Data == null}, DevicesStatus is null: {result?.Data?.DevicesStatus == null}");

                if (result != null && result.Data != null && result.Data.DevicesStatus != null)
                {
                    DebugLogger.Log($"Shelly API: Found {result.Data.DevicesStatus.Count} devices in dictionary");

                    // Convert dictionary to list and set IDs from keys
                    var devicesList = new List<ShellyDevice>();
                    foreach (var kvp in result.Data.DevicesStatus)
                    {
                        var device = kvp.Value;
                        device.Id = kvp.Key; // Set ID from dictionary key (MAC address)

                        // Generate name if empty
                        if (string.IsNullOrEmpty(device.Name) && device.GetInfo?.FwInfo?.Device != null)
                        {
                            var deviceModelName = device.GetInfo.FwInfo.Device;
                            var shortMac = device.Id.Length > 4 ? device.Id.Substring(device.Id.Length - 4) : device.Id;
                            device.Name = $"{deviceModelName} ({shortMac})";
                        }
                        else if (string.IsNullOrEmpty(device.Name))
                        {
                            device.Name = device.Id;
                        }

                        // Create Status object for backward compatibility
                        if (device.Status == null)
                        {
                            device.Status = new DeviceStatus
                            {
                                Lights = device.Lights,
                                Relays = device.Relays,
                                Thermostats = device.Thermostats
                            };
                        }

                        devicesList.Add(device);
                        var deviceType = device.GetDeviceType();
                        DebugLogger.Log($"Shelly API: Added device {device.Id} ({device.Name}) - Type: {deviceType}");
                        DebugLogger.Log($"  - Has GetInfo: {device.GetInfo != null}, Has Lights: {device.Lights != null && device.Lights.Count > 0}, Has Relays: {device.Relays != null && device.Relays.Count > 0}, Has Thermostats: {device.Thermostats != null && device.Thermostats.Count > 0}");
                    }

                    DebugLogger.Log($"Shelly API: Returning {devicesList.Count} devices");
                    return devicesList;
                }

                DebugLogger.Log($"Shelly API: No devices found (null response data)");
                System.Windows.Forms.MessageBox.Show(
                    $"API Response received but no devices found.\n\nResponse preview:\n{content.Substring(0, Math.Min(300, content.Length))}\n\nCheck log file at:\n%LocalAppData%\\Loupedeck\\Logs\\ShellyPlugin_Debug.log",
                    "Shelly API - No Devices",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Warning
                );
                return new List<ShellyDevice>();
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"Shelly API ERROR: {ex.GetType().Name}: {ex.Message}");
                DebugLogger.Log($"Shelly API ERROR Stack: {ex.StackTrace}");
                throw; // Re-throw so caller can handle
            }
        }

        public async Task<ShellyDevice> GetDeviceStatusAsync(string deviceId)
        {
            if (!IsConfigured)
                throw new InvalidOperationException("API client not configured");

            await RateLimitAsync();

            try
            {
                var url = $"{_serverUrl}/device/status?auth_key={_authKey}&id={deviceId}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<DeviceStatusResponse>(content);

                if (result != null && result.Data != null)
                {
                    return result.Data.DeviceStatus;
                }
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting device status: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> SetRelayStateAsync(string deviceId, int channel, bool turnOn)
        {
            if (!IsConfigured)
                throw new InvalidOperationException("API client not configured");

            await RateLimitAsync();

            try
            {
                var turn = turnOn ? "on" : "off";
                var url = $"{_serverUrl}/device/relay/control?auth_key={_authKey}&id={deviceId}&channel={channel}&turn={turn}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting relay state: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SetLightBrightnessAsync(string deviceId, int brightness, int? channel = null)
        {
            if (!IsConfigured)
                throw new InvalidOperationException("API client not configured");

            await RateLimitAsync();

            try
            {
                var channelParam = channel.HasValue ? $"&channel={channel.Value}" : "";
                var url = $"{_serverUrl}/device/light/control?auth_key={_authKey}&id={deviceId}&turn=on&brightness={brightness}{channelParam}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting light brightness: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SetLightColorAsync(string deviceId, int red, int green, int blue, int white, int? channel = null)
        {
            if (!IsConfigured)
                throw new InvalidOperationException("API client not configured");

            await RateLimitAsync();

            try
            {
                var channelParam = channel.HasValue ? $"&channel={channel.Value}" : "";
                var url = $"{_serverUrl}/device/light/control?auth_key={_authKey}&id={deviceId}&turn=on&red={red}&green={green}&blue={blue}&white={white}{channelParam}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting light color: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SetThermostatTemperatureAsync(string deviceId, double temperature)
        {
            if (!IsConfigured)
                throw new InvalidOperationException("API client not configured");

            await RateLimitAsync();

            try
            {
                var url = $"{_serverUrl}/device/thermostat/set_target_t?auth_key={_authKey}&id={deviceId}&target_t_enabled=1&target_t_value={temperature}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting thermostat temperature: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SetThermostatBoostAsync(string deviceId, int minutes)
        {
            if (!IsConfigured)
                throw new InvalidOperationException("API client not configured");

            await RateLimitAsync();

            try
            {
                var url = $"{_serverUrl}/device/thermostat/boost?auth_key={_authKey}&id={deviceId}&boost_minutes={minutes}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting thermostat boost: {ex.Message}");
                return false;
            }
        }
    }

    public class DeviceListResponse
    {
        [JsonProperty("isok")]
        public bool IsOk { get; set; }

        [JsonProperty("data")]
        public DeviceListData Data { get; set; }
    }

    public class DeviceListData
    {
        [JsonProperty("devices_status")]
        public Dictionary<string, ShellyDevice> DevicesStatus { get; set; } = new Dictionary<string, ShellyDevice>();
    }

    public class DeviceStatusResponse
    {
        [JsonProperty("isok")]
        public bool IsOk { get; set; }

        [JsonProperty("data")]
        public DeviceStatusData Data { get; set; }
    }

    public class DeviceStatusData
    {
        [JsonProperty("device_status")]
        public ShellyDevice DeviceStatus { get; set; }
    }
}
