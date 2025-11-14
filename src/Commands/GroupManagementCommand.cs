using System;
using Loupedeck;
using ShellyLoupedeckPlugin.UI;

namespace ShellyLoupedeckPlugin.Commands
{
    public class GroupManagementCommand : PluginDynamicCommand
    {
        private ShellyLoupedeckPlugin _plugin;

        public GroupManagementCommand()
            : base("Manage Groups", "Open group management dialog", "Configuration")
        {
        }

        protected override bool OnLoad()
        {
            _plugin = (ShellyLoupedeckPlugin)Plugin;
            return true;
        }

        protected override void RunCommand(string actionParameter)
        {
            try
            {
                DebugLogger.Log("=== GroupManagementCommand: Opening group management dialog ===");

                var dialog = new GroupManagementDialog(_plugin);
                dialog.ShowDialog();

                DebugLogger.Log("GroupManagementCommand: Dialog closed");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"GroupManagementCommand ERROR: {ex.Message}");
                DebugLogger.Log($"GroupManagementCommand ERROR: {ex.StackTrace}");
            }
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmapBuilder = new BitmapBuilder(imageSize))
            {
                bitmapBuilder.Clear(new BitmapColor(64, 64, 64));
                bitmapBuilder.DrawText("Manage\nGroups", BitmapColor.White);
                return bitmapBuilder.ToImage();
            }
        }
    }
}
