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

                // Show confirmation
                System.Windows.Forms.MessageBox.Show(
                    "Settings saved! The plugin will now try to load your devices.\n\n" +
                    "If you don't see your devices, please check:\n" +
                    "- Server URL is correct (e.g., https://shelly-28-eu.shelly.cloud)\n" +
                    "- Authorization Key is correct\n" +
                    "- You have internet connection\n\n" +
                    "Check the Loupedeck logs for any errors.",
                    "Shelly Settings Saved",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Information
                );
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
