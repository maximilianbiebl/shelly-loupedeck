using System;
using Loupedeck;
using ShellyLoupedeckPlugin.UI;

namespace ShellyLoupedeckPlugin.Commands
{
    public class FolderBuilderCommand : PluginDynamicCommand
    {
        private ShellyLoupedeckPlugin _plugin;

        public FolderBuilderCommand()
            : base("Folder Builder", "Create and customize control folders", "Folders")
        {
            DebugLogger.Log("=== FolderBuilderCommand constructor called ===");
        }

        protected override bool OnLoad()
        {
            DebugLogger.Log("=== FolderBuilderCommand OnLoad called ===");
            _plugin = (ShellyLoupedeckPlugin)Plugin;
            return true;
        }

        protected override void RunCommand(string actionParameter)
        {
            try
            {
                DebugLogger.Log("=== FolderBuilderCommand: Opening folder builder dialog ===");

                var dialog = new FolderBuilderDialog(_plugin);
                dialog.ShowDialog();

                DebugLogger.Log("FolderBuilderCommand: Dialog closed");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"FolderBuilderCommand ERROR: {ex.Message}");
                DebugLogger.Log($"FolderBuilderCommand ERROR: {ex.StackTrace}");
            }
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmapBuilder = new BitmapBuilder(imageSize))
            {
                bitmapBuilder.Clear(new BitmapColor(100, 150, 200));
                bitmapBuilder.DrawText("Folder\nBuilder", BitmapColor.White);
                return bitmapBuilder.ToImage();
            }
        }
    }
}
