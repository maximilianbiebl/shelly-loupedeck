using System;
using System.Linq;
using Loupedeck;
using ShellyLoupedeckPlugin.Models;

namespace ShellyLoupedeckPlugin.Actions
{
    /// <summary>
    /// Device Overview Command - Similar to task-switcher, shows all devices in touch display
    /// </summary>
    public class DeviceOverviewCommand : PluginDynamicCommand
    {
        private ShellyLoupedeckPlugin _plugin;
        private bool _isOverviewActive = false;

        public DeviceOverviewCommand() : base()
        {
            DisplayName = "Device Overview";
            Description = "Show all Shelly devices in a grid view";
            GroupName = "Navigation";
        }

        protected override bool OnLoad()
        {
            _plugin = (ShellyLoupedeckPlugin)Plugin;
            _plugin.DevicesUpdated += OnDevicesUpdated;

            CreateParameters();

            return base.OnLoad();
        }

        protected override bool OnUnload()
        {
            _plugin.DevicesUpdated -= OnDevicesUpdated;
            return base.OnUnload();
        }

        private void OnDevicesUpdated(object sender, EventArgs e)
        {
            CreateParameters();
        }

        private void CreateParameters()
        {
            RemoveAllParameters();

            // Main overview button
            AddParameter("overview", "All Devices", "Overview");

            // Category overviews
            AddParameter("switches", "All Switches", "Categories");
            AddParameter("lights", "All Lights", "Categories");
            AddParameter("thermostats", "All Thermostats", "Categories");
            AddParameter("groups", "All Groups", "Categories");

            ActionImageChanged();
        }

        protected override void RunCommand(string actionParameter)
        {
            // This would typically open a dynamic page showing all devices
            // For now, we just toggle the overview state
            _isOverviewActive = !_isOverviewActive;
            ActionImageChanged(actionParameter);
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var builder = new BitmapBuilder(imageSize))
            {
                if (string.IsNullOrEmpty(actionParameter))
                {
                    builder.Clear(BitmapColor.Black);
                    builder.DrawText("Overview");
                    return builder.ToImage();
                }

                switch (actionParameter)
                {
                    case "overview":
                        builder.Clear(_isOverviewActive ? new BitmapColor(0, 150, 255) : BitmapColor.Black);
                        builder.DrawText($"Devices\n({_plugin.Devices.Count})", BitmapColor.White);
                        break;

                    case "switches":
                        var switchCount = _plugin.Devices.Count(d =>
                            d.GetDeviceType() == ShellyDeviceType.Switch ||
                            d.GetDeviceType() == ShellyDeviceType.ShellyPlus2PM);
                        builder.Clear(BitmapColor.Black);
                        builder.DrawText($"Switches\n({switchCount})", BitmapColor.White);
                        break;

                    case "lights":
                        var lightCount = _plugin.Devices.Count(d =>
                            d.GetDeviceType() == ShellyDeviceType.RGBW ||
                            d.GetDeviceType() == ShellyDeviceType.Dimmer);
                        builder.Clear(BitmapColor.Black);
                        builder.DrawText($"Lights\n({lightCount})", BitmapColor.White);
                        break;

                    case "thermostats":
                        var thermostatCount = _plugin.Devices.Count(d =>
                            d.GetDeviceType() == ShellyDeviceType.Thermostat);
                        builder.Clear(BitmapColor.Black);
                        builder.DrawText($"Thermostats\n({thermostatCount})", BitmapColor.White);
                        break;

                    case "groups":
                        builder.Clear(BitmapColor.Black);
                        builder.DrawText($"Groups\n({_plugin.Groups.Count})", BitmapColor.White);
                        break;

                    default:
                        builder.Clear(BitmapColor.Black);
                        break;
                }

                return builder.ToImage();
            }
        }
    }
}
