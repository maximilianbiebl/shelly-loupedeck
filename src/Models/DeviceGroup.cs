using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ShellyLoupedeckPlugin.Models
{
    public class DeviceGroup
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("deviceIds")]
        public List<string> DeviceIds { get; set; } = new List<string>();

        [JsonProperty("purpose")]
        public GroupPurpose Purpose { get; set; }

        // Backward compatibility: migrate old "type" field to "purpose"
        [JsonProperty("type")]
        private ShellyDeviceType _legacyType
        {
            get => ShellyDeviceType.Unknown; // Never used for serialization
            set
            {
                // Convert old device type to new purpose
                if (Purpose == default(GroupPurpose))
                {
                    Purpose = value switch
                    {
                        // RGBW groups become Brightness groups (most versatile - works for dimming and with Dimmers too)
                        ShellyDeviceType.RGBW => GroupPurpose.Brightness,
                        ShellyDeviceType.Dimmer => GroupPurpose.Brightness,
                        ShellyDeviceType.Switch => GroupPurpose.Switch,
                        ShellyDeviceType.ShellyPlus2PM => GroupPurpose.Switch,
                        ShellyDeviceType.Thermostat => GroupPurpose.Thermostat,
                        _ => GroupPurpose.Switch
                    };
                }
            }
        }

        public DeviceGroup()
        {
        }

        public DeviceGroup(string name, GroupPurpose purpose)
        {
            Name = name;
            Purpose = purpose;
        }
    }
}
