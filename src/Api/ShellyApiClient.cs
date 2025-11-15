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

        private async Task<HttpResponseMessage> PostWithRetryAsync(string url, Func<HttpContent> contentFactory, string operationName)
        {
            const int maxRetries = 2; // Total 3 attempts (1 initial + 2 retries)
            int attempt = 0;

            while (attempt <= maxRetries)
            {
                try
                {
                    // Create fresh content for each attempt (HttpContent can only be used once)
                    using (var content = contentFactory())
                    {
                        var response = await _httpClient.PostAsync(url, content);

                        // Check if it's a rate limit error
                        if ((response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                             (int)response.StatusCode == 429) && attempt < maxRetries)
                        {
                            var responseContent = await response.Content.ReadAsStringAsync();
                            DebugLogger.Log($"  {operationName}: Rate limit error ({response.StatusCode}), attempt {attempt + 1}/{maxRetries + 1}. Waiting 2s and retrying...");
                            DebugLogger.Log($"  {operationName}: Response content: {responseContent.Substring(0, Math.Min(200, responseContent.Length))}");

                            attempt++;
                            await Task.Delay(2000); // Wait 2 seconds before retry
                            continue;
                        }

                        return response;
                    }
                }
                catch (HttpRequestException ex)
                {
                    if (attempt < maxRetries)
                    {
                        DebugLogger.Log($"  {operationName}: HTTP error on attempt {attempt + 1}/{maxRetries + 1}: {ex.Message}. Waiting 2s and retrying...");
                        attempt++;
                        await Task.Delay(2000);
                        continue;
                    }
                    throw;
                }
                catch (TaskCanceledException ex)
                {
                    if (attempt < maxRetries)
                    {
                        DebugLogger.Log($"  {operationName}: Timeout on attempt {attempt + 1}/{maxRetries + 1}: {ex.Message}. Waiting 2s and retrying...");
                        attempt++;
                        await Task.Delay(2000);
                        continue;
                    }
                    throw;
                }
            }

            // Should never reach here, but just in case
            throw new Exception($"{operationName}: Max retries exceeded");
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
                    var listUrl = $"{_serverUrl}/device/list?auth_key={_authKey}";
                    var listResponse = await _httpClient.GetAsync(listUrl);

                    if (listResponse.IsSuccessStatusCode)
                    {
                        var listContent = await listResponse.Content.ReadAsStringAsync();
                        var deviceListResult = JsonConvert.DeserializeObject<DeviceListResponse>(listContent);

                        if (deviceListResult?.Data?.Devices != null)
                        {
                            foreach (var deviceInfo in deviceListResult.Data.Devices)
                            {
                                if (!string.IsNullOrWhiteSpace(deviceInfo.Name))
                                {
                                    deviceNames[deviceInfo.Id] = deviceInfo.Name;
                                }
                            }
                        }
                    }
                    else
                    {
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"Shelly API: /device/list not available ({ex.Message}), will use fallback names");
                }

                // Step 2: Get device statuses from /device/all_status
                var url = $"{_serverUrl}/device/all_status?auth_key={_authKey}";
                var response = await _httpClient.GetAsync(url);

                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();

                var result = JsonConvert.DeserializeObject<AllStatusResponse>(content);


                if (result != null && result.Data != null && result.Data.DevicesStatus != null)
                {

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
                        }
                        else if (device.Settings != null && !string.IsNullOrWhiteSpace(device.Settings.Name))
                        {
                            device.Name = device.Settings.Name;
                        }
                        else if (string.IsNullOrWhiteSpace(device.Name))
                        {
                            // Generate name from device type if available
                            if (device.GetInfo?.FwInfo?.Device != null)
                            {
                                var deviceModelName = device.GetInfo.FwInfo.Device;
                                var shortMac = device.Id.Length > 4 ? device.Id.Substring(device.Id.Length - 4) : device.Id;
                                device.Name = $"{deviceModelName} ({shortMac})";
                            }
                            else
                            {
                                device.Name = device.Id;
                            }
                        }
                        else
                        {
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
                            }
                        }

                        devicesList.Add(device);
                        var deviceType = device.GetDeviceType();
                    }

                    return devicesList;
                }

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


                // Create content factory for retry mechanism
                var response = await PostWithRetryAsync(url, () => new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("auth_key", _authKey),
                    new KeyValuePair<string, string>("id", deviceId),
                    new KeyValuePair<string, string>("channel", channel.ToString()),
                    new KeyValuePair<string, string>("turn", turn)
                }), "SetRelayStateAsync");
                var responseContent = await response.Content.ReadAsStringAsync();

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


                // Create content factory for retry mechanism
                var response = await PostWithRetryAsync(url, () => new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("auth_key", _authKey),
                    new KeyValuePair<string, string>("id", deviceId),
                    new KeyValuePair<string, string>("channel", channel.ToString()),
                    new KeyValuePair<string, string>("turn", turn)
                }), "SetGen3SwitchStateAsync");
                var responseContent = await response.Content.ReadAsStringAsync();

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


                // Create content factory for retry mechanism
                var response = await PostWithRetryAsync(url, () => new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("auth_key", _authKey),
                    new KeyValuePair<string, string>("id", deviceId),
                    new KeyValuePair<string, string>("channel", channel.ToString()),
                    new KeyValuePair<string, string>("turn", turn)
                }), "SetLightStateAsync");
                var responseContent = await response.Content.ReadAsStringAsync();

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

                var channelStr = channel.HasValue ? $"&channel={channel.Value}" : "";

                // Create content factory for retry mechanism
                var response = await PostWithRetryAsync(url, () =>
                {
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

                    return new FormUrlEncodedContent(formParams);
                }, "SetLightBrightnessAsync");
                var responseContent = await response.Content.ReadAsStringAsync();

                response.EnsureSuccessStatusCode();
                return true;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"  SetLightBrightnessAsync ERROR: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SetLightColorAsync(string deviceId, int red, int green, int blue, int white, int? channel = null, int? temperature = null, int? brightness = null)
        {
            if (!IsConfigured)
                throw new InvalidOperationException("API client not configured");

            await RateLimitAsync();

            try
            {
                var url = $"{_serverUrl}/device/light/control";

                // Determine required mode based on color values
                // If using RGB colors (any R/G/B > 0), use color mode
                // If using white only (R=G=B=0, W > 0), use white mode
                string mode = null;
                if (red > 0 || green > 0 || blue > 0)
                {
                    mode = "color";
                }
                else if (red == 0 && green == 0 && blue == 0 && white > 0)
                {
                    mode = "white";
                }

                var channelStr = channel.HasValue ? $"&channel={channel.Value}" : "";
                var modeStr = mode != null ? $"&mode={mode}" : "";
                var tempStr = temperature.HasValue && mode == "white" ? $"&temp={temperature.Value}" : "";
                var brightnessStr = brightness.HasValue ? (mode == "color" ? $"&gain={brightness.Value}" : $"&brightness={brightness.Value}") : "";

                // Create content factory for retry mechanism
                var response = await PostWithRetryAsync(url, () =>
                {
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

                    // Add mode parameter if detected
                    if (mode != null)
                    {
                        formParams.Add(new KeyValuePair<string, string>("mode", mode));
                    }

                    // Add temperature parameter for white mode if specified
                    if (temperature.HasValue && mode == "white")
                    {
                        formParams.Add(new KeyValuePair<string, string>("temp", temperature.Value.ToString()));
                    }

                    // Add brightness/gain parameter based on mode
                    if (brightness.HasValue)
                    {
                        if (mode == "color")
                        {
                            // Color mode uses 'gain' parameter for brightness control
                            formParams.Add(new KeyValuePair<string, string>("gain", brightness.Value.ToString()));
                        }
                        else
                        {
                            // White mode uses 'brightness' parameter
                            formParams.Add(new KeyValuePair<string, string>("brightness", brightness.Value.ToString()));
                        }
                    }

                    return new FormUrlEncodedContent(formParams);
                }, "SetLightColorAsync");
                var responseContent = await response.Content.ReadAsStringAsync();

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


                // Create content factory for retry mechanism
                var response = await PostWithRetryAsync(url, () => new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("auth_key", _authKey),
                    new KeyValuePair<string, string>("id", deviceId),
                    new KeyValuePair<string, string>("temp", temperature.ToString(CultureInfo.InvariantCulture))
                }), "SetThermostatTemperatureAsync");
                var responseContent = await response.Content.ReadAsStringAsync();

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


                // Create content factory for retry mechanism
                var response = await PostWithRetryAsync(url, () => new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("auth_key", _authKey),
                    new KeyValuePair<string, string>("id", deviceId),
                    new KeyValuePair<string, string>("boost_minutes", minutes.ToString())
                }), "SetThermostatBoostAsync");
                var responseContent = await response.Content.ReadAsStringAsync();

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
