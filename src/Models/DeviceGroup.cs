using Newtonsoft.Json;

namespace ShellyLoupedeckPlugin.Models;

public class DeviceGroup
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("deviceIds")]
    public List<string> DeviceIds { get; set; } = new List<string>();

    [JsonProperty("type")]
    public DeviceType Type { get; set; }

    public DeviceGroup()
    {
    }

    public DeviceGroup(string name, DeviceType type)
    {
        Name = name;
        Type = type;
    }
}
