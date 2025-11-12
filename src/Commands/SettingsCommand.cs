using System;
using Loupedeck;
using ShellyLoupedeckPlugin.UI;

namespace ShellyLoupedeckPlugin.Commands
{
    public class SettingsCommand : PluginDynamicCommand
    {
        private ShellyLoupedeckPlugin _plugin;

        public SettingsCommand() : base()
        {
            DisplayName = "Shelly Settings";
            Description = "Configure Shelly Cloud API connection";
            GroupName = "Settings";
        }

        protected override bool OnLoad()
        {
            _plugin = (ShellyLoupedeckPlugin)Plugin;

            // Add a default parameter so the command is visible
            AddParameter("configure", "Configure API", "Settings");

            return base.OnLoad();
        }

        protected override void RunCommand(string actionParameter)
        {
            // Get current settings
            var currentServerUrl = "";
            var currentAuthKey = "";

            if (_plugin.TryGetPluginSetting("ServerUrl", out var serverUrl))
            {
                currentServerUrl = serverUrl;
            }

            if (_plugin.TryGetPluginSetting("AuthKey", out var authKey))
            {
                currentAuthKey = authKey;
            }

            // Open settings dialog
            var dialog = new SettingsDialog(currentServerUrl, currentAuthKey);
            dialog.ShowDialog();

            // Save settings if user clicked Save
            if (dialog.SaveClicked)
            {
                _plugin.SaveConfiguration(dialog.ServerUrl, dialog.AuthKey);
            }
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmapBuilder = new BitmapBuilder(imageSize))
            {
                bitmapBuilder.Clear(BitmapColor.Black);
                bitmapBuilder.DrawText("Settings");
                return bitmapBuilder.ToImage();
            }
        }
    }
}
