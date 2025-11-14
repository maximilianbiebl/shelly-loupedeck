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
            get { return ShellyDeviceType.Unknown; } // Never used for serialization
            set
            {
                // Convert old device type to new purpose
                if (Purpose == default(GroupPurpose))
                {
                    switch (value)
                    {
                        // Keep RGBW as Color groups (user can create separate Brightness groups if needed)
                        case ShellyDeviceType.RGBW:
                            Purpose = GroupPurpose.Color;
                            break;
                        case ShellyDeviceType.Dimmer:
                            Purpose = GroupPurpose.Brightness;
                            break;
                        case ShellyDeviceType.Switch:
                            Purpose = GroupPurpose.Switch;
                            break;
                        case ShellyDeviceType.ShellyPlus2PM:
                            Purpose = GroupPurpose.Switch;
                            break;
                        case ShellyDeviceType.Thermostat:
                            Purpose = GroupPurpose.Thermostat;
                            break;
                        default:
                            Purpose = GroupPurpose.Switch;
                            break;
                    }
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
