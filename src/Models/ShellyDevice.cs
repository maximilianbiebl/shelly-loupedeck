using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ShellyLoupedeckPlugin.Models
{
    public class ShellyDevice
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        [JsonProperty("app")]
        public string App { get; set; } = string.Empty;

        [JsonProperty("online")]
        public bool Online { get; set; }

        // Old structure compatibility
        [JsonProperty("status")]
        public DeviceStatus Status { get; set; }

        [JsonProperty("settings")]
        public DeviceSettings Settings { get; set; }

        // Direct fields from all_status response
        [JsonProperty("lights")]
        public List<LightStatus> Lights { get; set; }

        [JsonProperty("relays")]
        public List<RelayStatus> Relays { get; set; }

        [JsonProperty("thermostats")]
        public List<ThermostatStatus> Thermostats { get; set; }

        [JsonProperty("getinfo")]
        public GetInfoData GetInfo { get; set; }

        [JsonProperty("mac")]
        public string Mac { get; set; } = string.Empty;

        // Gen 3 device components (component-based structure)
        [JsonProperty("switch:0")]
        public Gen3Component Switch0 { get; set; }

        [JsonProperty("switch:1")]
        public Gen3Component Switch1 { get; set; }

        [JsonProperty("sys")]
        public Gen3SysInfo Sys { get; set; }

        // Gen 4 light components. Dimmers and lights expose "light:N" instead of
        // the "switch:N" used by relay-style component devices.
        [JsonProperty("light:0")]
        public LightComponent Light0 { get; set; }

        [JsonProperty("light:1")]
        public LightComponent Light1 { get; set; }

        public ShellyDeviceType GetDeviceType()
        {
            // Component-based devices (Gen 3/4) must be classified by their actual
            // component. The "sys" block is shared by every component device, so the
            // switch branch below would otherwise claim dimmers and lights too.
            if (Light0 != null || Light1 != null)
            {
                var light = Light0 ?? Light1;
                return (light.Rgb != null && light.Rgb.Count >= 3)
                    ? ShellyDeviceType.RGBW
                    : ShellyDeviceType.Dimmer;
            }

            if (Switch0 != null || Switch1 != null || Sys != null)
            {
                // Component-based relay device
                return ShellyDeviceType.Switch;
            }

            // Try to get type from getinfo first (all_status response)
            if (GetInfo?.FwInfo?.Device != null)
            {
                var deviceType = GetInfo.FwInfo.Device.ToLower();
                if (deviceType.Contains("rgbw")) return ShellyDeviceType.RGBW;
                if (deviceType.Contains("dimmer")) return ShellyDeviceType.Dimmer;
                if (deviceType.Contains("trv") || deviceType.Contains("thermostat")) return ShellyDeviceType.Thermostat;
                if (deviceType.Contains("switch") || deviceType.Contains("plus") || deviceType.Contains("mini")) return ShellyDeviceType.Switch;
            }

            // Fallback to old Type property
            if (!string.IsNullOrEmpty(Type))
            {
                if (Type.Contains("RGBW2") || (App != null && App.Equals("RGBW", StringComparison.OrdinalIgnoreCase)))
                    return ShellyDeviceType.RGBW;
                if (Type.IndexOf("Dimmer", StringComparison.OrdinalIgnoreCase) >= 0)
                    return ShellyDeviceType.Dimmer;
                if (Type.IndexOf("TRV", StringComparison.OrdinalIgnoreCase) >= 0 || Type.IndexOf("Thermostat", StringComparison.OrdinalIgnoreCase) >= 0)
                    return ShellyDeviceType.Thermostat;
                if (Type.IndexOf("SNSW", StringComparison.OrdinalIgnoreCase) >= 0 || Type.IndexOf("Plus 2PM", StringComparison.OrdinalIgnoreCase) >= 0)
                    return ShellyDeviceType.ShellyPlus2PM;
                if (Type.IndexOf("Relay", StringComparison.OrdinalIgnoreCase) >= 0 || Type.IndexOf("Switch", StringComparison.OrdinalIgnoreCase) >= 0)
                    return ShellyDeviceType.Switch;
            }

            // Check by capabilities - if has lights but no relays, it's likely RGBW
            if (Lights != null && Lights.Count > 0)
            {
                // Has lights - could be RGBW or Dimmer
                if (Relays == null || Relays.Count == 0)
                {
                    // No relays - likely RGBW bulb
                    return ShellyDeviceType.RGBW;
                }
                else
                {
                    // Has both lights and relays - likely Dimmer
                    return ShellyDeviceType.Dimmer;
                }
            }

            // Check if has relays only - likely a switch
            if (Relays != null && Relays.Count > 0)
            {
                return ShellyDeviceType.Switch;
            }

            // Check if has thermostats
            if (Thermostats != null && Thermostats.Count > 0)
            {
                return ShellyDeviceType.Thermostat;
            }

            return ShellyDeviceType.Unknown;
        }
    }

    public class GetInfoData
    {
        [JsonProperty("fw_info")]
        public FirmwareInfo FwInfo { get; set; }
    }

    public class FirmwareInfo
    {
        [JsonProperty("device")]
        public string Device { get; set; } = string.Empty;

        [JsonProperty("fw")]
        public string Firmware { get; set; } = string.Empty;
    }

    public class DeviceStatus
    {
        [JsonProperty("lights")]
        public List<LightStatus> Lights { get; set; }

        [JsonProperty("relays")]
        public List<RelayStatus> Relays { get; set; }

        [JsonProperty("meters")]
        public List<MeterStatus> Meters { get; set; }

        [JsonProperty("tmp")]
        public TemperatureStatus Temperature { get; set; }

        [JsonProperty("thermostats")]
        public List<ThermostatStatus> Thermostats { get; set; }
    }

    public class DeviceSettings
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("device")]
        public DeviceInfo Device { get; set; }
    }

    public class DeviceInfo
    {
        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        [JsonProperty("hostname")]
        public string Hostname { get; set; } = string.Empty;
    }

    public class LightStatus
    {
        [JsonProperty("ison")]
        public bool IsOn { get; set; }

        [JsonProperty("brightness")]
        public int Brightness { get; set; }

        [JsonProperty("red")]
        public int Red { get; set; }

        [JsonProperty("green")]
        public int Green { get; set; }

        [JsonProperty("blue")]
        public int Blue { get; set; }

        [JsonProperty("white")]
        public int White { get; set; }

        [JsonProperty("gain")]
        public int Gain { get; set; }
    }

    public class RelayStatus
    {
        [JsonProperty("ison")]
        public bool IsOn { get; set; }

        [JsonProperty("has_timer")]
        public bool HasTimer { get; set; }

        [JsonProperty("timer_remaining")]
        public int TimerRemaining { get; set; }
    }

    public class MeterStatus
    {
        [JsonProperty("power")]
        public double Power { get; set; }

        [JsonProperty("total")]
        public double Total { get; set; }
    }

    public class TemperatureStatus
    {
        [JsonProperty("tC")]
        public double TemperatureCelsius { get; set; }

        [JsonProperty("tF")]
        public double TemperatureFahrenheit { get; set; }

        [JsonProperty("is_valid")]
        public bool IsValid { get; set; }
    }

    public class ThermostatStatus
    {
        [JsonProperty("target_t")]
        public TargetTemperature TargetTemperature { get; set; }

        [JsonProperty("tmp")]
        public CurrentTemperature CurrentTemperature { get; set; }

        [JsonProperty("boost_minutes")]
        public int BoostMinutes { get; set; }

        [JsonProperty("schedule")]
        public bool Schedule { get; set; }
    }

    public class TargetTemperature
    {
        [JsonProperty("enabled")]
        public bool Enabled { get; set; }

        [JsonProperty("value")]
        public double Value { get; set; }

        [JsonProperty("units")]
        public string Units { get; set; } = "C";
    }

    public class CurrentTemperature
    {
        [JsonProperty("value")]
        public double Value { get; set; }

        [JsonProperty("units")]
        public string Units { get; set; } = "C";

        [JsonProperty("is_valid")]
        public bool IsValid { get; set; }
    }

    public class Gen3Component
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("output")]
        public bool Output { get; set; }

        [JsonProperty("source")]
        public string Source { get; set; } = string.Empty;
    }

    /// <summary>
    /// A "light:N" component as used by Gen 4 dimmers and lights. Colour-capable
    /// devices additionally populate <see cref="Rgb"/>.
    /// </summary>
    public class LightComponent
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("output")]
        public bool Output { get; set; }

        [JsonProperty("brightness")]
        public double Brightness { get; set; }

        [JsonProperty("source")]
        public string Source { get; set; } = string.Empty;

        [JsonProperty("rgb")]
        public List<int> Rgb { get; set; }

        [JsonProperty("white")]
        public int? White { get; set; }

        /// <summary>Projects this component onto the legacy light status shape.</summary>
        public LightStatus ToLightStatus()
        {
            var status = new LightStatus
            {
                IsOn = Output,
                Brightness = (int)Math.Round(Brightness)
            };

            if (Rgb != null && Rgb.Count >= 3)
            {
                status.Red = Rgb[0];
                status.Green = Rgb[1];
                status.Blue = Rgb[2];
            }

            if (White.HasValue)
                status.White = White.Value;

            return status;
        }
    }

    public class Gen3SysInfo
    {
        [JsonProperty("mac")]
        public string Mac { get; set; } = string.Empty;

        [JsonProperty("model")]
        public string Model { get; set; } = string.Empty;

        [JsonProperty("fw_id")]
        public string FirmwareId { get; set; } = string.Empty;
    }

    public enum ShellyDeviceType
    {
        Unknown,
        Switch,
        RGBW,
        Dimmer,
        Thermostat,
        ShellyPlus2PM
    }
}
