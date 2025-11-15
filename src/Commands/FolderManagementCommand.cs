using System;
using System.Linq;
using Loupedeck;
using ShellyLoupedeckPlugin.Models;

namespace ShellyLoupedeckPlugin.Commands
{
    public class FolderManagementCommand : PluginDynamicCommand
    {
        private ShellyLoupedeckPlugin _plugin;

        public FolderManagementCommand()
        {
            DisplayName = "Folder Management";
            Description = "Create and manage custom control folders";
            GroupName = "Configuration";
        }

        protected override bool OnLoad()
        {
            _plugin = (ShellyLoupedeckPlugin)Plugin;
            CreateParameters();
            return base.OnLoad();
        }

        private void CreateParameters()
        {
            RemoveAllParameters();
            AddParameter("create_example", "Create Example Folder", "Actions");
            AddParameter("create_color_folder", "Create Color Folder", "Actions");
            AddParameter("create_switch_folder", "Create Switch Folder", "Actions");
        }

        protected override void RunCommand(string actionParameter)
        {
            if (string.IsNullOrEmpty(actionParameter))
                return;

            switch (actionParameter)
            {
                case "create_example":
                    CreateExampleFolder();
                    break;

                case "create_color_folder":
                    CreateColorFolder();
                    break;

                case "create_switch_folder":
                    CreateSwitchFolder();
                    break;
            }
        }

        private void CreateExampleFolder()
        {
            var folder = new FolderConfiguration
            {
                Name = "Example Folder",
                Buttons = new System.Collections.Generic.List<FolderButton>()
            };

            // Add first color group with color presets
            var colorGroup = _plugin.Groups.FirstOrDefault(g => g.Purpose == GroupPurpose.Color);
            if (colorGroup != null)
            {
                folder.Buttons.Add(new FolderButton(FolderButtonType.GroupColor, colorGroup.Id, "red", "Red"));
                folder.Buttons.Add(new FolderButton(FolderButtonType.GroupColor, colorGroup.Id, "green", "Green"));
                folder.Buttons.Add(new FolderButton(FolderButtonType.GroupColor, colorGroup.Id, "blue", "Blue"));
                folder.Buttons.Add(new FolderButton(FolderButtonType.GroupBrightness, colorGroup.Id, "50", "50%"));
                folder.Buttons.Add(new FolderButton(FolderButtonType.GroupBrightness, colorGroup.Id, "100", "100%"));
            }

            // Add first switch group toggle
            var switchGroup = _plugin.Groups.FirstOrDefault(g => g.Purpose == GroupPurpose.Switch);
            if (switchGroup != null)
            {
                folder.Buttons.Add(new FolderButton(FolderButtonType.GroupToggle, switchGroup.Id, null, "All Switches"));
            }

            _plugin.AddFolder(folder);

            System.Windows.Forms.MessageBox.Show(
                $"Example folder '{folder.Name}' created!\n\n" +
                "You can now add it to your touchfield via 'Group Controls'.",
                "Folder Created",
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Information
            );
        }

        private void CreateColorFolder()
        {
            var colorGroup = _plugin.Groups.FirstOrDefault(g => g.Purpose == GroupPurpose.Color);
            if (colorGroup == null)
            {
                System.Windows.Forms.MessageBox.Show(
                    "No color group found. Please create a color group first.",
                    "Error",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error
                );
                return;
            }

            var folder = new FolderConfiguration
            {
                Name = $"{colorGroup.Name} Colors",
                Buttons = new System.Collections.Generic.List<FolderButton>
                {
                    new FolderButton(FolderButtonType.GroupColor, colorGroup.Id, "red"),
                    new FolderButton(FolderButtonType.GroupColor, colorGroup.Id, "green"),
                    new FolderButton(FolderButtonType.GroupColor, colorGroup.Id, "blue"),
                    new FolderButton(FolderButtonType.GroupColor, colorGroup.Id, "white"),
                    new FolderButton(FolderButtonType.GroupColor, colorGroup.Id, "yellow"),
                    new FolderButton(FolderButtonType.GroupColor, colorGroup.Id, "cyan"),
                    new FolderButton(FolderButtonType.GroupColor, colorGroup.Id, "magenta"),
                    new FolderButton(FolderButtonType.GroupBrightness, colorGroup.Id, "25"),
                    new FolderButton(FolderButtonType.GroupBrightness, colorGroup.Id, "50"),
                    new FolderButton(FolderButtonType.GroupBrightness, colorGroup.Id, "75"),
                    new FolderButton(FolderButtonType.GroupBrightness, colorGroup.Id, "100")
                }
            };

            _plugin.AddFolder(folder);

            System.Windows.Forms.MessageBox.Show(
                $"Color folder '{folder.Name}' created!",
                "Folder Created",
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Information
            );
        }

        private void CreateSwitchFolder()
        {
            var switchGroup = _plugin.Groups.FirstOrDefault(g => g.Purpose == GroupPurpose.Switch);
            if (switchGroup == null)
            {
                System.Windows.Forms.MessageBox.Show(
                    "No switch group found. Please create a switch group first.",
                    "Error",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error
                );
                return;
            }

            var folder = new FolderConfiguration
            {
                Name = $"{switchGroup.Name} Devices",
                Buttons = new System.Collections.Generic.List<FolderButton>()
            };

            // Add toggle for each device
            foreach (var deviceId in switchGroup.DeviceIds)
            {
                var device = _plugin.Devices.FirstOrDefault(d => d.Id == deviceId);
                if (device != null)
                {
                    folder.Buttons.Add(new FolderButton(FolderButtonType.DeviceToggle, deviceId, "ch0"));
                }
            }

            // Add group toggle
            folder.Buttons.Add(new FolderButton(FolderButtonType.GroupToggle, switchGroup.Id, null, "ALL"));

            _plugin.AddFolder(folder);

            System.Windows.Forms.MessageBox.Show(
                $"Switch folder '{folder.Name}' created!",
                "Folder Created",
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Information
            );
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var builder = new BitmapBuilder(imageSize))
            {
                builder.Clear(new BitmapColor(100, 100, 200));

                if (actionParameter == "create_example")
                    builder.DrawText("Example", BitmapColor.White, 14);
                else if (actionParameter == "create_color_folder")
                    builder.DrawText("Colors", BitmapColor.White, 14);
                else if (actionParameter == "create_switch_folder")
                    builder.DrawText("Switches", BitmapColor.White, 14);
                else
                    builder.DrawText("Folder", BitmapColor.White, 14);

                return builder.ToImage();
            }
        }
    }
}
