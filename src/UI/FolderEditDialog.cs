using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ShellyLoupedeckPlugin.Models;

namespace ShellyLoupedeckPlugin.UI
{
    public class FolderEditDialog : Form
    {
        private ShellyLoupedeckPlugin _plugin;
        private FolderConfiguration _folder;
        private bool _isNewFolder;

        private TextBox _nameTextBox;
        private ListBox _buttonsListBox;
        private Button _addDeviceButton;
        private Button _addGroupButton;
        private Button _addActionButton;
        private Button _removeButton;
        private Button _moveUpButton;
        private Button _moveDownButton;
        private Button _saveButton;
        private Button _cancelButton;

        public FolderEditDialog(ShellyLoupedeckPlugin plugin, FolderConfiguration folder)
        {
            _plugin = plugin;
            _isNewFolder = (folder == null);

            if (_isNewFolder)
            {
                _folder = new FolderConfiguration
                {
                    Name = $"Folder {_plugin.Folders.Count + 1}",
                    Buttons = new System.Collections.Generic.List<FolderButton>()
                };
            }
            else
            {
                _folder = folder;
            }

            InitializeUI();
            LoadButtons();
        }

        private void InitializeUI()
        {
            Text = _isNewFolder ? "Create New Folder" : $"Edit Folder: {_folder.Name}";
            Size = new Size(600, 500);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            // Name
            var nameLabel = new Label
            {
                Text = "Folder Name:",
                Location = new Point(10, 15),
                Size = new Size(100, 20)
            };
            Controls.Add(nameLabel);

            _nameTextBox = new TextBox
            {
                Text = _folder.Name,
                Location = new Point(120, 12),
                Size = new Size(460, 25)
            };
            Controls.Add(_nameTextBox);

            // Buttons list
            var buttonsLabel = new Label
            {
                Text = "Buttons (max 9 for 3x3 grid):",
                Location = new Point(10, 50),
                Size = new Size(580, 20)
            };
            Controls.Add(buttonsLabel);

            _buttonsListBox = new ListBox
            {
                Location = new Point(10, 75),
                Size = new Size(580, 280)
            };
            _buttonsListBox.SelectedIndexChanged += ButtonsListBox_SelectedIndexChanged;
            Controls.Add(_buttonsListBox);

            // Add buttons
            _addDeviceButton = new Button
            {
                Text = "Add Device",
                Location = new Point(10, 365),
                Size = new Size(120, 30)
            };
            _addDeviceButton.Click += AddDeviceButton_Click;
            Controls.Add(_addDeviceButton);

            _addGroupButton = new Button
            {
                Text = "Add Group",
                Location = new Point(140, 365),
                Size = new Size(120, 30)
            };
            _addGroupButton.Click += AddGroupButton_Click;
            Controls.Add(_addGroupButton);

            _addActionButton = new Button
            {
                Text = "Add Action",
                Location = new Point(270, 365),
                Size = new Size(120, 30)
            };
            _addActionButton.Click += AddActionButton_Click;
            Controls.Add(_addActionButton);

            // Modify buttons
            _removeButton = new Button
            {
                Text = "Remove",
                Location = new Point(400, 365),
                Size = new Size(90, 30),
                Enabled = false
            };
            _removeButton.Click += RemoveButton_Click;
            Controls.Add(_removeButton);

            _moveUpButton = new Button
            {
                Text = "↑",
                Location = new Point(500, 365),
                Size = new Size(40, 30),
                Enabled = false
            };
            _moveUpButton.Click += MoveUpButton_Click;
            Controls.Add(_moveUpButton);

            _moveDownButton = new Button
            {
                Text = "↓",
                Location = new Point(550, 365),
                Size = new Size(40, 30),
                Enabled = false
            };
            _moveDownButton.Click += MoveDownButton_Click;
            Controls.Add(_moveDownButton);

            // Save/Cancel
            _saveButton = new Button
            {
                Text = "Save",
                Location = new Point(370, 420),
                Size = new Size(100, 35)
            };
            _saveButton.Click += SaveButton_Click;
            Controls.Add(_saveButton);

            _cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(480, 420),
                Size = new Size(100, 35)
            };
            _cancelButton.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(_cancelButton);
        }

        private void LoadButtons()
        {
            _buttonsListBox.Items.Clear();

            for (int i = 0; i < _folder.Buttons.Count; i++)
            {
                var button = _folder.Buttons[i];
                _buttonsListBox.Items.Add($"{i + 1}. {button.CustomLabel ?? "(Unlabeled)"}");
            }

            UpdateButtonStates();
        }

        private void ButtonsListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateButtonStates();
        }

        private void UpdateButtonStates()
        {
            bool hasSelection = _buttonsListBox.SelectedIndex >= 0;
            bool canMoveUp = hasSelection && _buttonsListBox.SelectedIndex > 0;
            bool canMoveDown = hasSelection && _buttonsListBox.SelectedIndex < _folder.Buttons.Count - 1;

            _removeButton.Enabled = hasSelection;
            _moveUpButton.Enabled = canMoveUp;
            _moveDownButton.Enabled = canMoveDown;
        }

        private void AddDeviceButton_Click(object sender, EventArgs e)
        {
            if (_folder.Buttons.Count >= 9)
            {
                MessageBox.Show("Maximum 9 buttons per folder.", "Limit Reached", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var devices = _plugin.Devices.ToList();
            if (devices.Count == 0)
            {
                MessageBox.Show("No devices available.", "No Devices", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selector = new SimpleSelectionDialog("Select Device", devices.Select(d => d.Name).ToList());
            if (selector.ShowDialog() == DialogResult.OK && selector.SelectedIndex >= 0)
            {
                var device = devices[selector.SelectedIndex];

                // Ask for custom label
                string customLabel = PromptForInput("Button Label", $"Enter label for '{device.Name}':", device.Name);
                if (customLabel == null) return; // User cancelled

                _folder.Buttons.Add(new FolderButton(FolderButtonType.DeviceToggle, device.Id, "ch0", customLabel));
                LoadButtons();
            }
        }

        private void AddGroupButton_Click(object sender, EventArgs e)
        {
            if (_folder.Buttons.Count >= 9)
            {
                MessageBox.Show("Maximum 9 buttons per folder.", "Limit Reached", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var groups = _plugin.Groups.ToList();
            if (groups.Count == 0)
            {
                MessageBox.Show("No groups available.", "No Groups", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selector = new SimpleSelectionDialog("Select Group", groups.Select(g => $"{g.Name} ({g.Purpose})").ToList());
            if (selector.ShowDialog() == DialogResult.OK && selector.SelectedIndex >= 0)
            {
                var group = groups[selector.SelectedIndex];
                _folder.Buttons.Add(new FolderButton(FolderButtonType.GroupToggle, group.Id, null, $"{group.Name} ALL"));
                LoadButtons();
            }
        }

        private void AddActionButton_Click(object sender, EventArgs e)
        {
            if (_folder.Buttons.Count >= 9)
            {
                MessageBox.Show("Maximum 9 buttons per folder.", "Limit Reached", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var actions = new System.Collections.Generic.List<string>();
            var actionData = new System.Collections.Generic.List<(string actionName, string actionParam, string label)>();

            // Device-specific actions
            foreach (var device in _plugin.Devices)
            {
                var deviceType = device.GetDeviceType();

                // All devices get switch action
                actions.Add($"{device.Name} - Switch");
                actionData.Add(("DeviceSwitchAction", device.Id, $"{device.Name} - Switch"));

                // RGBW devices
                if (deviceType == ShellyDeviceType.RGBW)
                {
                    actions.Add($"{device.Name} - Color");
                    actionData.Add(("RGBWColorAdjustment", device.Id, $"{device.Name} - Color"));

                    actions.Add($"{device.Name} - Mode Toggle");
                    actionData.Add(("RGBWModeToggle", device.Id, $"{device.Name} - Mode"));

                    actions.Add($"{device.Name} - Brightness");
                    actionData.Add(("RGBWBrightnessAdjustment", device.Id, $"{device.Name} - Bright"));
                }

                // Dimmer devices
                if (deviceType == ShellyDeviceType.Dimmer || deviceType == ShellyDeviceType.RGBW)
                {
                    actions.Add($"{device.Name} - Dimmer");
                    actionData.Add(("DimmerAdjustment", device.Id, $"{device.Name} - Dim"));
                }

                // Thermostat devices
                if (deviceType == ShellyDeviceType.Thermostat)
                {
                    actions.Add($"{device.Name} - Temperature");
                    actionData.Add(("ThermostatAdjustment", device.Id, $"{device.Name} - Temp"));

                    actions.Add($"{device.Name} - Boost");
                    actionData.Add(("ThermostatBoostAction", device.Id, $"{device.Name} - Boost"));
                }
            }

            // Management commands
            actions.Add("Device Overview");
            actionData.Add(("DeviceOverviewCommand", "", "Overview"));

            actions.Add("Group Management");
            actionData.Add(("GroupManagementCommand", "", "Groups"));

            actions.Add("Settings");
            actionData.Add(("SettingsCommand", "", "Settings"));

            if (actions.Count == 0)
            {
                MessageBox.Show("No actions available.", "No Actions", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selector = new SimpleSelectionDialog("Select Action", actions);
            if (selector.ShowDialog() == DialogResult.OK && selector.SelectedIndex >= 0)
            {
                var selected = actionData[selector.SelectedIndex];
                _folder.Buttons.Add(new FolderButton(selected.actionName, selected.actionParam, selected.label));
                LoadButtons();
            }
        }

        private void RemoveButton_Click(object sender, EventArgs e)
        {
            if (_buttonsListBox.SelectedIndex < 0)
                return;

            _folder.Buttons.RemoveAt(_buttonsListBox.SelectedIndex);
            LoadButtons();
        }

        private void MoveUpButton_Click(object sender, EventArgs e)
        {
            int index = _buttonsListBox.SelectedIndex;
            if (index <= 0)
                return;

            var button = _folder.Buttons[index];
            _folder.Buttons.RemoveAt(index);
            _folder.Buttons.Insert(index - 1, button);
            LoadButtons();
            _buttonsListBox.SelectedIndex = index - 1;
        }

        private void MoveDownButton_Click(object sender, EventArgs e)
        {
            int index = _buttonsListBox.SelectedIndex;
            if (index < 0 || index >= _folder.Buttons.Count - 1)
                return;

            var button = _folder.Buttons[index];
            _folder.Buttons.RemoveAt(index);
            _folder.Buttons.Insert(index + 1, button);
            LoadButtons();
            _buttonsListBox.SelectedIndex = index + 1;
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_nameTextBox.Text))
            {
                MessageBox.Show("Please enter a folder name.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _folder.Name = _nameTextBox.Text.Trim();

            if (_isNewFolder)
            {
                _plugin.AddFolder(_folder);
            }
            else
            {
                _plugin.UpdateFolder(_folder);
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private string PromptForInput(string title, string prompt, string defaultValue)
        {
            Form inputForm = new Form
            {
                Width = 400,
                Height = 150,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = title,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false
            };

            Label textLabel = new Label { Left = 10, Top = 15, Width = 360, Text = prompt };
            TextBox textBox = new TextBox { Left = 10, Top = 40, Width = 360, Text = defaultValue };
            Button okButton = new Button { Text = "OK", Left = 200, Width = 80, Top = 75, DialogResult = DialogResult.OK };
            Button cancelButton = new Button { Text = "Cancel", Left = 290, Width = 80, Top = 75, DialogResult = DialogResult.Cancel };

            okButton.Click += (sender, e) => { inputForm.Close(); };
            cancelButton.Click += (sender, e) => { inputForm.Close(); };

            inputForm.Controls.Add(textLabel);
            inputForm.Controls.Add(textBox);
            inputForm.Controls.Add(okButton);
            inputForm.Controls.Add(cancelButton);
            inputForm.AcceptButton = okButton;
            inputForm.CancelButton = cancelButton;

            textBox.SelectAll();

            return inputForm.ShowDialog() == DialogResult.OK ? textBox.Text.Trim() : null;
        }
    }

    // Simple selection dialog helper
    public class SimpleSelectionDialog : Form
    {
        public int SelectedIndex { get; private set; } = -1;

        public SimpleSelectionDialog(string title, System.Collections.Generic.List<string> items)
        {
            Text = title;
            Size = new Size(400, 350);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var listBox = new ListBox
            {
                Location = new Point(10, 10),
                Size = new Size(370, 250)
            };
            foreach (var item in items)
            {
                listBox.Items.Add(item);
            }
            listBox.DoubleClick += (s, e) =>
            {
                if (listBox.SelectedIndex >= 0)
                {
                    SelectedIndex = listBox.SelectedIndex;
                    DialogResult = DialogResult.OK;
                    Close();
                }
            };
            Controls.Add(listBox);

            var okButton = new Button
            {
                Text = "OK",
                Location = new Point(220, 270),
                Size = new Size(75, 30)
            };
            okButton.Click += (s, e) =>
            {
                SelectedIndex = listBox.SelectedIndex;
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(okButton);

            var cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(305, 270),
                Size = new Size(75, 30)
            };
            cancelButton.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            Controls.Add(cancelButton);
        }
    }
}
