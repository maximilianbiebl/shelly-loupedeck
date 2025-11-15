using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ShellyLoupedeckPlugin.Models;

namespace ShellyLoupedeckPlugin.UI
{
    public class FolderBuilderDialog : Form
    {
        private ShellyLoupedeckPlugin _plugin;
        private ListBox _folderListBox;
        private Button _createButton;
        private Button _editButton;
        private Button _deleteButton;
        private Button _closeButton;

        public FolderBuilderDialog(ShellyLoupedeckPlugin plugin)
        {
            _plugin = plugin;
            InitializeUI();
            LoadFolders();
        }

        private void InitializeUI()
        {
            Text = "Folder Builder";
            Size = new Size(500, 400);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            // Folder list
            var label = new Label
            {
                Text = "Custom Folders:",
                Location = new Point(10, 10),
                Size = new Size(480, 20)
            };
            Controls.Add(label);

            _folderListBox = new ListBox
            {
                Location = new Point(10, 35),
                Size = new Size(480, 250),
                SelectionMode = SelectionMode.One
            };
            _folderListBox.SelectedIndexChanged += FolderListBox_SelectedIndexChanged;
            Controls.Add(_folderListBox);

            // Buttons
            _createButton = new Button
            {
                Text = "Create New Folder",
                Location = new Point(10, 295),
                Size = new Size(150, 30)
            };
            _createButton.Click += CreateButton_Click;
            Controls.Add(_createButton);

            _editButton = new Button
            {
                Text = "Edit Folder",
                Location = new Point(170, 295),
                Size = new Size(150, 30),
                Enabled = false
            };
            _editButton.Click += EditButton_Click;
            Controls.Add(_editButton);

            _deleteButton = new Button
            {
                Text = "Delete Folder",
                Location = new Point(330, 295),
                Size = new Size(150, 30),
                Enabled = false
            };
            _deleteButton.Click += DeleteButton_Click;
            Controls.Add(_deleteButton);

            _closeButton = new Button
            {
                Text = "Close",
                Location = new Point(330, 335),
                Size = new Size(150, 30)
            };
            _closeButton.Click += (s, e) => Close();
            Controls.Add(_closeButton);

            // Info label
            var infoLabel = new Label
            {
                Text = "Create folders with custom buttons. Use Custom Folder 1-10 actions to display them.",
                Location = new Point(10, 335),
                Size = new Size(310, 40),
                ForeColor = Color.Gray
            };
            Controls.Add(infoLabel);
        }

        private void LoadFolders()
        {
            _folderListBox.Items.Clear();

            for (int i = 0; i < _plugin.Folders.Count; i++)
            {
                var folder = _plugin.Folders[i];
                _folderListBox.Items.Add($"{i + 1}. {folder.Name} ({folder.Buttons.Count} buttons)");
            }

            if (_folderListBox.Items.Count == 0)
            {
                _folderListBox.Items.Add("(No folders yet - click 'Create New Folder')");
            }
        }

        private void FolderListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            bool hasSelection = _folderListBox.SelectedIndex >= 0 &&
                               _folderListBox.SelectedIndex < _plugin.Folders.Count;

            _editButton.Enabled = hasSelection;
            _deleteButton.Enabled = hasSelection;
        }

        private void CreateButton_Click(object sender, EventArgs e)
        {
            if (_plugin.Folders.Count >= 10)
            {
                MessageBox.Show(
                    "Maximum of 10 folders reached.\n\nDelete an existing folder to create a new one.",
                    "Folder Limit Reached",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                return;
            }

            var dialog = new FolderEditDialog(_plugin, null);
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                LoadFolders();
            }
        }

        private void EditButton_Click(object sender, EventArgs e)
        {
            if (_folderListBox.SelectedIndex < 0 || _folderListBox.SelectedIndex >= _plugin.Folders.Count)
                return;

            var folder = _plugin.Folders[_folderListBox.SelectedIndex];
            var dialog = new FolderEditDialog(_plugin, folder);
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                LoadFolders();
            }
        }

        private void DeleteButton_Click(object sender, EventArgs e)
        {
            if (_folderListBox.SelectedIndex < 0 || _folderListBox.SelectedIndex >= _plugin.Folders.Count)
                return;

            var folder = _plugin.Folders[_folderListBox.SelectedIndex];

            var result = MessageBox.Show(
                $"Delete folder '{folder.Name}'?\n\nThis cannot be undone.",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (result == DialogResult.Yes)
            {
                _plugin.RemoveFolder(folder.Id);
                LoadFolders();
            }
        }
    }
}
