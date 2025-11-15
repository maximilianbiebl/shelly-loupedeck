using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ShellyLoupedeckPlugin.Models;

namespace ShellyLoupedeckPlugin.UI
{
    public class FlexibleFolderBuilderDialog : Form
    {
        private ShellyLoupedeckPlugin _plugin;
        private ListBox _foldersListBox;
        private Button _newFolderButton;
        private Button _editFolderButton;
        private Button _deleteFolderButton;
        private Button _createExampleButton;
        private Button _closeButton;

        public FlexibleFolderBuilderDialog(ShellyLoupedeckPlugin plugin)
        {
            _plugin = plugin;
            InitializeUI();
            LoadFolders();
        }

        private void InitializeUI()
        {
            Text = "Flexible Folder Manager";
            Size = new Size(600, 500);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var titleLabel = new Label
            {
                Text = "Flexible Folders",
                Location = new Point(20, 20),
                Size = new Size(560, 25),
                Font = new Font("Arial", 14, FontStyle.Bold)
            };
            Controls.Add(titleLabel);

            var infoLabel = new Label
            {
                Text = "Create custom multi-level folder structures with unlimited navigation depth.\nOnly labels YOU set will be displayed - no auto device names.",
                Location = new Point(20, 50),
                Size = new Size(560, 40)
            };
            Controls.Add(infoLabel);

            // Folders list
            _foldersListBox = new ListBox
            {
                Location = new Point(20, 100),
                Size = new Size(560, 280)
            };
            _foldersListBox.SelectedIndexChanged += (s, e) => UpdateButtonStates();
            _foldersListBox.DoubleClick += (s, e) => EditFolderButton_Click(s, e);
            Controls.Add(_foldersListBox);

            // Folder management buttons
            _newFolderButton = new Button
            {
                Text = "New Folder",
                Location = new Point(20, 395),
                Size = new Size(110, 35)
            };
            _newFolderButton.Click += NewFolderButton_Click;
            Controls.Add(_newFolderButton);

            _editFolderButton = new Button
            {
                Text = "Edit",
                Location = new Point(140, 395),
                Size = new Size(80, 35),
                Enabled = false
            };
            _editFolderButton.Click += EditFolderButton_Click;
            Controls.Add(_editFolderButton);

            _deleteFolderButton = new Button
            {
                Text = "Delete",
                Location = new Point(230, 395),
                Size = new Size(80, 35),
                Enabled = false
            };
            _deleteFolderButton.Click += DeleteFolderButton_Click;
            Controls.Add(_deleteFolderButton);

            _createExampleButton = new Button
            {
                Text = "Create Example",
                Location = new Point(340, 395),
                Size = new Size(130, 35)
            };
            _createExampleButton.Click += CreateExampleButton_Click;
            Controls.Add(_createExampleButton);

            _closeButton = new Button
            {
                Text = "Close",
                Location = new Point(480, 395),
                Size = new Size(100, 35)
            };
            _closeButton.Click += (s, e) => Close();
            Controls.Add(_closeButton);
        }

        private void LoadFolders()
        {
            _foldersListBox.Items.Clear();
            foreach (var folder in _plugin.FlexibleFolders)
            {
                _foldersListBox.Items.Add(folder.Name);
            }
            UpdateButtonStates();
        }

        private void UpdateButtonStates()
        {
            bool hasSelection = _foldersListBox.SelectedIndex >= 0;
            _editFolderButton.Enabled = hasSelection;
            _deleteFolderButton.Enabled = hasSelection;
        }

        private void NewFolderButton_Click(object sender, EventArgs e)
        {
            var newFolder = new FlexibleFolderConfiguration();
            var editor = new FlexibleFolderEditorDialog(_plugin, newFolder);
            if (editor.ShowDialog() == DialogResult.OK)
            {
                LoadFolders();
            }
        }

        private void EditFolderButton_Click(object sender, EventArgs e)
        {
            int index = _foldersListBox.SelectedIndex;
            if (index < 0) return;

            var folder = _plugin.FlexibleFolders[index];
            var editor = new FlexibleFolderEditorDialog(_plugin, folder);
            if (editor.ShowDialog() == DialogResult.OK)
            {
                LoadFolders();
            }
        }

        private void DeleteFolderButton_Click(object sender, EventArgs e)
        {
            int index = _foldersListBox.SelectedIndex;
            if (index < 0) return;

            var folder = _plugin.FlexibleFolders[index];
            var result = MessageBox.Show(
                $"Delete folder '{folder.Name}'?",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                _plugin.RemoveFlexibleFolder(folder.Id);
                LoadFolders();
            }
        }

        private void CreateExampleButton_Click(object sender, EventArgs e)
        {
            // Create a sample flexible folder with navigation
            var folder = new FlexibleFolderConfiguration
            {
                Name = "Example Flexible Folder"
            };

            // Main level - add some devices and a submenu
            folder.RootLevel.Name = "Main Menu";

            // Add first 3 devices as toggles (with labels)
            var devices = _plugin.Devices.Take(3).ToList();
            for (int i = 0; i < devices.Count && i < 3; i++)
            {
                folder.RootLevel.Buttons.Add(new FlexibleButton(
                    $"Device {i + 1}", // Custom label
                    "DeviceToggle",
                    deviceId: devices[i].Id
                ));
            }

            // Create a submenu
            var subLevel = new FlexibleFolderLevel
            {
                Name = "More Devices"
            };

            // Add more devices to submenu (with labels)
            var moreDevices = _plugin.Devices.Skip(3).Take(4).ToList();
            for (int i = 0; i < moreDevices.Count; i++)
            {
                subLevel.Buttons.Add(new FlexibleButton(
                    $"Extra {i + 1}", // Custom label
                    "DeviceToggle",
                    deviceId: moreDevices[i].Id
                ));
            }

            // Add navigation button to main level
            if (moreDevices.Count > 0)
            {
                folder.RootLevel.Buttons.Add(new FlexibleButton(
                    "More →",
                    subLevel
                ));
            }

            // Open in editor for further customization
            var editor = new FlexibleFolderEditorDialog(_plugin, folder);
            if (editor.ShowDialog() == DialogResult.OK)
            {
                LoadFolders();
                MessageBox.Show(
                    "Example folder created and saved!\n\n" +
                    "Add 'Flexible Folder 1-10' to your Loupedeck to use it.\n\n" +
                    "Features:\n" +
                    "- Only your custom labels are shown\n" +
                    "- Navigation buttons (purple) open submenus\n" +
                    "- Toggle buttons (green/gray) control devices",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
        }
    }
}
