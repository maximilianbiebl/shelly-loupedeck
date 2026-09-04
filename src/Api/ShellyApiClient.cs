using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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

        // Device structure is logged once per session rather than on every refresh
        private bool _loggedRawDeviceFields = false;

        // Resolved device names, cached for the session (null until first lookup)
        private Dictionary<string, string> _deviceNames;

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

            // Names belong to the account, so a credential change invalidates them
            _deviceNames = null;
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
                            DebugLogger.Warn($"{operationName}: Rate limit error ({response.StatusCode}), attempt {attempt + 1}/{maxRetries + 1}. Waiting 2s and retrying...");
                            DebugLogger.Warn($"{operationName}: Response content: {responseContent.Substring(0, Math.Min(200, responseContent.Length))}");

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
                        DebugLogger.Warn($"{operationName}: HTTP error on attempt {attempt + 1}/{maxRetries + 1}: {ex.Message}. Waiting 2s and retrying...");
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
                        DebugLogger.Warn($"{operationName}: Timeout on attempt {attempt + 1}/{maxRetries + 1}: {ex.Message}. Waiting 2s and retrying...");
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
                // Step 1: Get the names the user assigned to their devices
                var deviceNames = await GetDeviceNamesAsync();

                // Step 2: Get device statuses from /device/all_status
                var url = $"{_serverUrl}/device/all_status?auth_key={_authKey}";
                var response = await _httpClient.GetAsync(url);

                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();

                var result = JsonConvert.DeserializeObject<AllStatusResponse>(content);

                // Log raw JSON field names per device once per session - the typed model
                // discards unknown fields, so this is the only way to see the structure
                // of device generations the model does not cover yet. Logging it on every
                // refresh would flood the log, so it runs on the first fetch only.
                var logDeviceStructure = !_loggedRawDeviceFields;
                if (logDeviceStructure)
                {
                    _loggedRawDeviceFields = true;
                    try
                    {
                        var rawRoot = JObject.Parse(content);
                        var rawDevices = rawRoot["data"]?["devices_status"] as JObject;
                        if (rawDevices != null)
                        {
                            foreach (var rawDevice in rawDevices)
                            {
                                var fields = (rawDevice.Value as JObject)?.Properties().Select(p => p.Name);
                                DebugLogger.Verbose($"  RAW {rawDevice.Key}: {string.Join(", ", fields ?? Enumerable.Empty<string>())}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.Warn($"Raw field logging failed: {ex.Message}");
                    }
                }

                if (result != null && result.Data != null && result.Data.DevicesStatus != null)
                {

                    // Convert dictionary to list and set IDs from keys
                    var devicesList = new List<ShellyDevice>();
                    foreach (var kvp in result.Data.DevicesStatus)
                    {
                        var device = kvp.Value;
                        device.Id = kvp.Key; // Set ID from dictionary key (MAC address)

                        // Resolve a display name: the name the user assigned in the Shelly
                        // app first, then the device's own settings, then a generated
                        // "<model> (<mac suffix>)" label. Component-based devices (Gen 3/4)
                        // report their model under "sys" rather than "getinfo", so both are
                        // consulted before falling back to the bare MAC address.
                        if (deviceNames.TryGetValue(device.Id, out var assignedName))
                        {
                            device.Name = assignedName;
                        }
                        else if (!string.IsNullOrWhiteSpace(device.Settings?.Name))
                        {
                            device.Name = device.Settings.Name;
                        }
                        else if (string.IsNullOrWhiteSpace(device.Name))
                        {
                            var model = device.GetInfo?.FwInfo?.Device;
                            if (string.IsNullOrWhiteSpace(model))
                                model = device.Sys?.Model;

                            var shortMac = device.Id.Length > 4
                                ? device.Id.Substring(device.Id.Length - 4)
                                : device.Id;

                            device.Name = string.IsNullOrWhiteSpace(model)
                                ? device.Id
                                : $"{model} ({shortMac})";
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

                            // For Gen 4 devices, convert light components to light status
                            if (device.Light0 != null && device.Lights == null)
                            {
                                device.Lights = new List<LightStatus> { device.Light0.ToLightStatus() };

                                if (device.Light1 != null)
                                    device.Lights.Add(device.Light1.ToLightStatus());

                                device.Status.Lights = device.Lights;
                            }
                        }

                        devicesList.Add(device);
                        var deviceType = device.GetDeviceType();

                        if (logDeviceStructure)
                        {
                            DebugLogger.Log($"  Device: {device.Name ?? device.Id} | Type: {deviceType} | Model: {device.GetInfo?.FwInfo?.Device ?? device.Type}");
                            DebugLogger.Log($"    Lights: {device.Lights?.Count ?? 0}, Relays: {device.Relays?.Count ?? 0}, Switch0: {device.Switch0 != null}, Light0: {device.Light0 != null}, Sys.Model: '{device.Sys?.Model}'");
                        }
                    }

                    return devicesList;
                }

                // No devices found - log only, no popup
                DebugLogger.Log("Shelly API: Response received but no devices found");
                DebugLogger.Log($"Response preview: {content.Substring(0, Math.Min(300, content.Length))}");
                return new List<ShellyDevice>();
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"{ex.GetType().Name}: {ex.Message}");
                DebugLogger.Verbose($"Stack: {ex.StackTrace}");
                throw; // Re-throw so caller can handle
            }
        }

        /// <summary>
        /// Fetches the names the user assigned to their devices in the Shelly app.
        /// The status endpoint does not carry them, and which endpoint serves them
        /// varies between Shelly Cloud deployments, so the known candidates are tried
        /// in order. The outcome is cached for the session: names rarely change, and
        /// re-probing a missing endpoint on every refresh would burn a request every
        /// few seconds against a rate-limited API.
        /// </summary>
        private async Task<Dictionary<string, string>> GetDeviceNamesAsync()
        {
            if (_deviceNames != null)
                return _deviceNames;

            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Ordered by how current each endpoint is; the first that yields names wins
            var candidates = new[]
            {
                new { Path = "/interface/device/get_all_lists", Post = true },
                new { Path = "/device/list", Post = false }
            };

            foreach (var candidate in candidates)
            {
                try
                {
                    await RateLimitAsync();

                    HttpResponseMessage response;
                    if (candidate.Post)
                    {
                        var form = new FormUrlEncodedContent(new[]
                        {
                            new KeyValuePair<string, string>("auth_key", _authKey)
                        });
                        response = await _httpClient.PostAsync($"{_serverUrl}{candidate.Path}", form);
                    }
                    else
                    {
                        response = await _httpClient.GetAsync($"{_serverUrl}{candidate.Path}?auth_key={_authKey}");
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        DebugLogger.Log($"Shelly API: {candidate.Path} returned {(int)response.StatusCode} {response.ReasonPhrase}");
                        continue;
                    }

                    ParseDeviceNames(await response.Content.ReadAsStringAsync(), names);

                    if (names.Count > 0)
                    {
                        DebugLogger.Log($"Shelly API: resolved {names.Count} device name(s) via {candidate.Path}");
                        break;
                    }

                    DebugLogger.Log($"Shelly API: {candidate.Path} answered but carried no names");
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"Shelly API: {candidate.Path} failed ({ex.GetType().Name}: {ex.Message})");
                }
            }

            if (names.Count == 0)
                DebugLogger.Log("Shelly API: no endpoint supplied device names, using generated labels");

            // Cached either way - an empty result must not trigger a retry every refresh
            _deviceNames = names;
            return names;
        }

        /// <summary>
        /// Extracts device id to user-assigned name pairs from a device list response.
        /// The endpoint returns "devices" either as an object keyed by device id or as
        /// an array of objects carrying their own id. Deserialising into a fixed shape
        /// throws on the other one and loses every name, so both are accepted here.
        /// </summary>
        private static void ParseDeviceNames(string listContent, Dictionary<string, string> into)
        {
            var devices = JObject.Parse(listContent)["data"]?["devices"];

            if (devices is JObject keyed)
            {
                foreach (var entry in keyed)
                {
                    var name = (entry.Value as JObject)?["name"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(name))
                        into[entry.Key] = name.Trim();
                }
            }
            else if (devices is JArray array)
            {
                foreach (var entry in array)
                {
                    if (!(entry is JObject item))
                        continue;

                    var id = item["id"]?.ToString();
                    var name = item["name"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name))
                        into[id] = name.Trim();
                }
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
                DebugLogger.Error($"SetRelayStateAsync: {ex.Message}");
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
                DebugLogger.Error($"SetGen3SwitchStateAsync: {ex.Message}");
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
                DebugLogger.Error($"SetLightStateAsync: {ex.Message}");
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
                DebugLogger.Error($"SetLightBrightnessAsync: {ex.Message}");
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
                DebugLogger.Error($"SetLightColorAsync: {ex.Message}");
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
                DebugLogger.Error($"SetThermostatTemperatureAsync: {ex.Message}");
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
                DebugLogger.Error($"SetThermostatBoostAsync: {ex.Message}");
                return false;
            }
        }
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
