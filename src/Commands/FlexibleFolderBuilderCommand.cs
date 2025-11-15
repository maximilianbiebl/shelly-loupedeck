using Loupedeck;

namespace ShellyLoupedeckPlugin.Commands
{
    public class FlexibleFolderBuilderCommand : PluginDynamicCommand
    {
        public FlexibleFolderBuilderCommand()
        {
            DisplayName = "Flexible Folder Builder";
            Description = "Create custom multi-level folder structures";
        }

        protected override bool OnLoad()
        {
            return true;
        }

        protected override void RunCommand(string actionParameter)
        {
            var plugin = (ShellyLoupedeckPlugin)Plugin;

            var dialog = new UI.FlexibleFolderBuilderDialog(plugin);
            dialog.ShowDialog();
        }

        protected override BitmapImage GetCommandImage(string actionParameter, int width, int height)
        {
            using (var builder = new BitmapBuilder(width, height))
            {
                builder.Clear(new BitmapColor(50, 100, 200));
                builder.DrawText("FLEX\nFOLDER", BitmapColor.White, 14);
                return builder.ToImage();
            }
        }
    }
}
