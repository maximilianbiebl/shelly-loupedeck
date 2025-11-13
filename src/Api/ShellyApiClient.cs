using System;
using System.Collections.Generic;
using System.Globalization;
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
                // Step 1: Try to get device names from /device/list (optional, may not exist)
                var deviceNames = new Dictionary<string, string>();
                try
                {
                    DebugLogger.Log($"Shelly API: Step 1 - Trying to get device names from /device/list");
                    var listUrl = $"{_serverUrl}/device/list?auth_key={_authKey}";
                    var listResponse = await _httpClient.GetAsync(listUrl);

                    if (listResponse.IsSuccessStatusCode)
                    {
                        var listContent = await listResponse.Content.ReadAsStringAsync();
                        var deviceListResult = JsonConvert.DeserializeObject<DeviceListResponse>(listContent);

                        if (deviceListResult?.Data?.Devices != null)
                        {
                            DebugLogger.Log($"Shelly API: Found {deviceListResult.Data.Devices.Count} devices in /device/list");
                            foreach (var deviceInfo in deviceListResult.Data.Devices)
                            {
                                if (!string.IsNullOrWhiteSpace(deviceInfo.Name))
                                {
                                    deviceNames[deviceInfo.Id] = deviceInfo.Name;
                                    DebugLogger.Log($"  Device {deviceInfo.Id}: Name = '{deviceInfo.Name}'");
                                }
                            }
                        }
                    }
                    else
                    {
                        DebugLogger.Log($"Shelly API: /device/list returned {listResponse.StatusCode}, skipping name lookup");
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"Shelly API: /device/list not available ({ex.Message}), will use fallback names");
                }

                // Step 2: Get device statuses from /device/all_status
                DebugLogger.Log($"Shelly API: Step 2 - Getting device statuses from /device/all_status");
                var url = $"{_serverUrl}/device/all_status?auth_key={_authKey}";
                var response = await _httpClient.GetAsync(url);
                DebugLogger.Log($"Shelly API: Response status {response.StatusCode}");

                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                DebugLogger.Log($"Shelly API: Response content length: {content.Length}");

                var result = JsonConvert.DeserializeObject<AllStatusResponse>(content);

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

                        // Use name from /device/list if available
                        if (deviceNames.ContainsKey(device.Id))
                        {
                            device.Name = deviceNames[device.Id];
                            DebugLogger.Log($"  Device {device.Id}: Using name from /device/list: '{device.Name}'");
                        }
                        else if (device.Settings != null && !string.IsNullOrWhiteSpace(device.Settings.Name))
                        {
                            device.Name = device.Settings.Name;
                            DebugLogger.Log($"  Device {device.Id}: Using Settings.Name: {device.Name}");
                        }
                        else if (string.IsNullOrWhiteSpace(device.Name))
                        {
                            // Generate name from device type if available
                            if (device.GetInfo?.FwInfo?.Device != null)
                            {
                                var deviceModelName = device.GetInfo.FwInfo.Device;
                                var shortMac = device.Id.Length > 4 ? device.Id.Substring(device.Id.Length - 4) : device.Id;
                                device.Name = $"{deviceModelName} ({shortMac})";
                                DebugLogger.Log($"  Device {device.Id}: Generated name from model: {device.Name}");
                            }
                            else
                            {
                                device.Name = device.Id;
                                DebugLogger.Log($"  Device {device.Id}: Using MAC as name: {device.Name}");
                            }
                        }
                        else
                        {
                            DebugLogger.Log($"  Device {device.Id}: Using existing name: {device.Name}");
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

                            // For Gen 3 devices, convert switch components to relay status
                            if (device.Switch0 != null && device.Relays == null)
                            {
                                device.Relays = new List<RelayStatus>
                                {
                                    new RelayStatus { IsOn = device.Switch0.Output }
                                };
                                device.Status.Relays = device.Relays;
                                DebugLogger.Log($"    -> Converted Gen3 Switch0 to Relay (IsOn={device.Switch0.Output})");
                            }
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
                var url = $"{_serverUrl}/device/relay/control";

                // Send parameters as form-urlencoded POST body (NOT query string)
                var formContent = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("auth_key", _authKey),
                    new KeyValuePair<string, string>("id", deviceId),
                    new KeyValuePair<string, string>("channel", channel.ToString()),
                    new KeyValuePair<string, string>("turn", turn)
                });

                DebugLogger.Log($"  SetRelayStateAsync: URL = {url}, Body = id={deviceId}&channel={channel}&turn={turn}");

                var response = await _httpClient.PostAsync(url, formContent);
                var responseContent = await response.Content.ReadAsStringAsync();
                DebugLogger.Log($"  SetRelayStateAsync: Response status = {response.StatusCode}, Content = {responseContent.Substring(0, Math.Min(200, responseContent.Length))}");

                response.EnsureSuccessStatusCode();
                return true;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"  SetRelayStateAsync ERROR: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SetGen3SwitchStateAsync(string deviceId, int channel, bool turnOn)
        {
            if (!IsConfigured)
                throw new InvalidOperationException("API client not configured");

            await RateLimitAsync();

            try
            {
                var turn = turnOn ? "on" : "off";
                var url = $"{_serverUrl}/device/relay/control";

                // Send parameters as form-urlencoded POST body (NOT query string)
                var formContent = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("auth_key", _authKey),
                    new KeyValuePair<string, string>("id", deviceId),
                    new KeyValuePair<string, string>("channel", channel.ToString()),
                    new KeyValuePair<string, string>("turn", turn)
                });

                DebugLogger.Log($"  SetGen3SwitchStateAsync: URL = {url}, Body = id={deviceId}&channel={channel}&turn={turn}");

                var response = await _httpClient.PostAsync(url, formContent);
                var responseContent = await response.Content.ReadAsStringAsync();
                DebugLogger.Log($"  SetGen3SwitchStateAsync: Response status = {response.StatusCode}, Content = {responseContent.Substring(0, Math.Min(200, responseContent.Length))}");

                response.EnsureSuccessStatusCode();
                return true;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"  SetGen3SwitchStateAsync ERROR: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SetLightStateAsync(string deviceId, int channel, bool turnOn)
        {
            if (!IsConfigured)
                throw new InvalidOperationException("API client not configured");

            await RateLimitAsync();

            try
            {
                var turn = turnOn ? "on" : "off";
                var url = $"{_serverUrl}/device/light/control";

                // Send parameters as form-urlencoded POST body (NOT query string)
                var formContent = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("auth_key", _authKey),
                    new KeyValuePair<string, string>("id", deviceId),
                    new KeyValuePair<string, string>("channel", channel.ToString()),
                    new KeyValuePair<string, string>("turn", turn)
                });

                DebugLogger.Log($"  SetLightStateAsync: URL = {url}, Body = id={deviceId}&channel={channel}&turn={turn}");

                var response = await _httpClient.PostAsync(url, formContent);
                var responseContent = await response.Content.ReadAsStringAsync();
                DebugLogger.Log($"  SetLightStateAsync: Response status = {response.StatusCode}, Content = {responseContent.Substring(0, Math.Min(200, responseContent.Length))}");

                response.EnsureSuccessStatusCode();
                return true;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"  SetLightStateAsync ERROR: {ex.Message}");
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
                var url = $"{_serverUrl}/device/light/control";

                var formParams = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("auth_key", _authKey),
                    new KeyValuePair<string, string>("id", deviceId),
                    new KeyValuePair<string, string>("turn", "on"),
                    new KeyValuePair<string, string>("brightness", brightness.ToString())
                };

                if (channel.HasValue)
                {
                    formParams.Add(new KeyValuePair<string, string>("channel", channel.Value.ToString()));
                }

                var channelStr = channel.HasValue ? $"&channel={channel.Value}" : "";
                DebugLogger.Log($"  SetLightBrightnessAsync: URL = {url}, Body = id={deviceId}&turn=on&brightness={brightness}{channelStr}");

                var formContent = new FormUrlEncodedContent(formParams);
                var response = await _httpClient.PostAsync(url, formContent);
                var responseContent = await response.Content.ReadAsStringAsync();
                DebugLogger.Log($"  SetLightBrightnessAsync: Response status = {response.StatusCode}, Content = {responseContent.Substring(0, Math.Min(200, responseContent.Length))}");

                response.EnsureSuccessStatusCode();
                return true;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"  SetLightBrightnessAsync ERROR: {ex.Message}");
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
                var url = $"{_serverUrl}/device/light/control";

                var formParams = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("auth_key", _authKey),
                    new KeyValuePair<string, string>("id", deviceId),
                    new KeyValuePair<string, string>("turn", "on"),
                    new KeyValuePair<string, string>("red", red.ToString()),
                    new KeyValuePair<string, string>("green", green.ToString()),
                    new KeyValuePair<string, string>("blue", blue.ToString()),
                    new KeyValuePair<string, string>("white", white.ToString())
                };

                if (channel.HasValue)
                {
                    formParams.Add(new KeyValuePair<string, string>("channel", channel.Value.ToString()));
                }

                var channelStr = channel.HasValue ? $"&channel={channel.Value}" : "";
                DebugLogger.Log($"  SetLightColorAsync: URL = {url}, Body = id={deviceId}&turn=on&red={red}&green={green}&blue={blue}&white={white}{channelStr}");

                var formContent = new FormUrlEncodedContent(formParams);
                var response = await _httpClient.PostAsync(url, formContent);
                var responseContent = await response.Content.ReadAsStringAsync();
                DebugLogger.Log($"  SetLightColorAsync: Response status = {response.StatusCode}, Content = {responseContent.Substring(0, Math.Min(200, responseContent.Length))}");

                response.EnsureSuccessStatusCode();
                return true;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"  SetLightColorAsync ERROR: {ex.Message}");
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
                var url = $"{_serverUrl}/device/thermostat/control";

                var formContent = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("auth_key", _authKey),
                    new KeyValuePair<string, string>("id", deviceId),
                    new KeyValuePair<string, string>("temp", temperature.ToString(CultureInfo.InvariantCulture))
                });

                DebugLogger.Log($"  SetThermostatTemperatureAsync: URL = {url}, Body = id={deviceId}&temp={temperature}");

                var response = await _httpClient.PostAsync(url, formContent);
                var responseContent = await response.Content.ReadAsStringAsync();
                DebugLogger.Log($"  SetThermostatTemperatureAsync: Response status = {response.StatusCode}, Content = {responseContent.Substring(0, Math.Min(200, responseContent.Length))}");

                response.EnsureSuccessStatusCode();
                return true;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"  SetThermostatTemperatureAsync ERROR: {ex.Message}");
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
                var url = $"{_serverUrl}/device/thermostat/boost";

                var formContent = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("auth_key", _authKey),
                    new KeyValuePair<string, string>("id", deviceId),
                    new KeyValuePair<string, string>("boost_minutes", minutes.ToString())
                });

                DebugLogger.Log($"  SetThermostatBoostAsync: URL = {url}, Body = id={deviceId}&boost_minutes={minutes}");

                var response = await _httpClient.PostAsync(url, formContent);
                var responseContent = await response.Content.ReadAsStringAsync();
                DebugLogger.Log($"  SetThermostatBoostAsync: Response status = {response.StatusCode}, Content = {responseContent.Substring(0, Math.Min(200, responseContent.Length))}");

                response.EnsureSuccessStatusCode();
                return true;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"  SetThermostatBoostAsync ERROR: {ex.Message}");
                return false;
            }
        }
    }

    public class DeviceListResponse
    {
        [JsonProperty("isok")]
        public bool IsOk { get; set; }

        [JsonProperty("data")]
        public DeviceListDataSimple Data { get; set; }
    }

    public class DeviceListDataSimple
    {
        [JsonProperty("devices")]
        public List<DeviceInfo> Devices { get; set; } = new List<DeviceInfo>();
    }

    public class DeviceInfo
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;
    }

    public class AllStatusResponse
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
