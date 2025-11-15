using System;
using Loupedeck;

namespace ShellyLoupedeckPlugin.Commands
{
    /// <summary>
    /// Simple folder configuration command - minimal implementation for testing
    /// </summary>
    public class FolderConfigCommand : PluginDynamicCommand
    {
        private ShellyLoupedeckPlugin _plugin;

        public FolderConfigCommand()
            : base("Folder Config", "Configure custom folders", "Folders")
        {
            DebugLogger.Log("!!! FOLDER CONFIG COMMAND CONSTRUCTOR !!!");
        }

        protected override bool OnLoad()
        {
            DebugLogger.Log("!!! FOLDER CONFIG COMMAND ONLOAD !!!");
            _plugin = (ShellyLoupedeckPlugin)Plugin;
            return true;
        }

        protected override void RunCommand(string actionParameter)
        {
            try
            {
                DebugLogger.Log("!!! FOLDER CONFIG COMMAND RUN !!!");

                var message = $"Folder Configuration\n\n" +
                              $"Current folders: {_plugin.Folders.Count}\n\n" +
                              $"This is a simplified folder config interface.\n" +
                              $"The full FolderBuilderCommand should provide detailed editing.";

                System.Windows.Forms.MessageBox.Show(
                    message,
                    "Folder Configuration",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"!!! FOLDER CONFIG ERROR: {ex.Message} !!!");
            }
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmapBuilder = new BitmapBuilder(imageSize))
            {
                bitmapBuilder.Clear(new BitmapColor(100, 150, 200));
                bitmapBuilder.DrawText("Folder\nConfig", BitmapColor.White);
                return bitmapBuilder.ToImage();
            }
        }
    }
}
