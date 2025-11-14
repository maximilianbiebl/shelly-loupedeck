namespace ShellyLoupedeckPlugin.Models
{
    /// <summary>
    /// Defines the purpose/function of a device group.
    /// This allows mixing different device types that support the same functionality.
    /// </summary>
    public enum GroupPurpose
    {
        /// <summary>
        /// On/Off switching - supports Switch, ShellyPlus2PM, RGBW, and Dimmer devices
        /// </summary>
        Switch,

        /// <summary>
        /// Brightness control - supports RGBW and Dimmer devices
        /// </summary>
        Brightness,

        /// <summary>
        /// Color control - supports RGBW devices only
        /// </summary>
        Color,

        /// <summary>
        /// Temperature control - supports Thermostat devices only
        /// </summary>
        Thermostat
    }
}
