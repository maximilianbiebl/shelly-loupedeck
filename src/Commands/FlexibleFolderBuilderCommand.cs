using Loupedeck;

namespace ShellyLoupedeckPlugin.Commands
{
    public class FlexibleFolderBuilderCommand : PluginDynamicCommand
    {
        public FlexibleFolderBuilderCommand()
            : base("Flexible Folder Builder", "Create custom multi-level folder structures", "Folders")
        {
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
    }
}
