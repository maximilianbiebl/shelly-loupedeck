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

        [JsonProperty("status")]
        public DeviceStatus Status { get; set; }

        [JsonProperty("settings")]
        public DeviceSettings Settings { get; set; }

        public ShellyDeviceType GetDeviceType()
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

            return ShellyDeviceType.Unknown;
        }
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
