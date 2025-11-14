using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using ShellyLoupedeckPlugin.Models;

namespace ShellyLoupedeckPlugin.UI
{
    public class GroupEditDialog : Form
    {
        private ShellyLoupedeckPlugin _plugin;
        private DeviceGroup _group;
        private TextBox nameTextBox;
        private ComboBox typeComboBox;
        private CheckedListBox devicesListBox;
        private Button saveButton;
        private Button cancelButton;
        private Label nameLabel;
        private Label typeLabel;
        private Label devicesLabel;
        private Label titleLabel;

        public DeviceGroup Group => _group;

        public GroupEditDialog(ShellyLoupedeckPlugin plugin, DeviceGroup existingGroup)
        {
            _plugin = plugin;
            _group = existingGroup ?? new DeviceGroup();
            InitializeComponents();
            LoadDeviceTypes();
            LoadDevices();

            if (existingGroup != null)
            {
                // Editing existing group
                nameTextBox.Text = _group.Name;
                typeComboBox.SelectedItem = _group.Type;

                // Check devices that are in the group
                for (int i = 0; i < devicesListBox.Items.Count; i++)
                {
                    var item = devicesListBox.Items[i] as DeviceListItem;
                    if (item != null && _group.DeviceIds.Contains(item.DeviceId))
                    {
                        devicesListBox.SetItemChecked(i, true);
                    }
                }
            }
        }

        private void InitializeComponents()
        {
            // Form settings
            this.Text = _group.Id == null || _group.Name == string.Empty ? "Add Device Group" : "Edit Device Group";
            this.Width = 500;
            this.Height = 550;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            // Title
            titleLabel = new Label();
            titleLabel.Text = "Configure Device Group";
            titleLabel.Font = new System.Drawing.Font("Segoe UI", 12, System.Drawing.FontStyle.Bold);
            titleLabel.Location = new System.Drawing.Point(20, 20);
            titleLabel.Size = new System.Drawing.Size(450, 30);
            this.Controls.Add(titleLabel);

            // Name Label
            nameLabel = new Label();
            nameLabel.Text = "Group Name:";
            nameLabel.Location = new System.Drawing.Point(20, 60);
            nameLabel.Size = new System.Drawing.Size(100, 20);
            this.Controls.Add(nameLabel);

            // Name TextBox
            nameTextBox = new TextBox();
            nameTextBox.Location = new System.Drawing.Point(20, 85);
            nameTextBox.Size = new System.Drawing.Size(440, 25);
            this.Controls.Add(nameTextBox);

            // Type Label
            typeLabel = new Label();
            typeLabel.Text = "Device Type:";
            typeLabel.Location = new System.Drawing.Point(20, 120);
            typeLabel.Size = new System.Drawing.Size(100, 20);
            this.Controls.Add(typeLabel);

            // Type ComboBox
            typeComboBox = new ComboBox();
            typeComboBox.Location = new System.Drawing.Point(20, 145);
            typeComboBox.Size = new System.Drawing.Size(440, 25);
            typeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            typeComboBox.SelectedIndexChanged += TypeComboBox_SelectedIndexChanged;
            this.Controls.Add(typeComboBox);

            // Devices Label
            devicesLabel = new Label();
            devicesLabel.Text = "Select Devices:";
            devicesLabel.Location = new System.Drawing.Point(20, 180);
            devicesLabel.Size = new System.Drawing.Size(150, 20);
            this.Controls.Add(devicesLabel);

            // Devices CheckedListBox
            devicesListBox = new CheckedListBox();
            devicesListBox.Location = new System.Drawing.Point(20, 205);
            devicesListBox.Size = new System.Drawing.Size(440, 240);
            devicesListBox.CheckOnClick = true;
            this.Controls.Add(devicesListBox);

            // Save Button
            saveButton = new Button();
            saveButton.Text = "Save";
            saveButton.Location = new System.Drawing.Point(280, 460);
            saveButton.Size = new System.Drawing.Size(85, 30);
            saveButton.Click += SaveButton_Click;
            this.Controls.Add(saveButton);

            // Cancel Button
            cancelButton = new Button();
            cancelButton.Text = "Cancel";
            cancelButton.Location = new System.Drawing.Point(375, 460);
            cancelButton.Size = new System.Drawing.Size(85, 30);
            cancelButton.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
            this.Controls.Add(cancelButton);
        }

        private void LoadDeviceTypes()
        {
            typeComboBox.Items.Clear();
            typeComboBox.Items.Add(ShellyDeviceType.RGBW);
            typeComboBox.Items.Add(ShellyDeviceType.Switch);
            typeComboBox.Items.Add(ShellyDeviceType.Dimmer);
            typeComboBox.Items.Add(ShellyDeviceType.Thermostat);

            if (_group.Type != ShellyDeviceType.Unknown)
            {
                typeComboBox.SelectedItem = _group.Type;
            }
            else if (typeComboBox.Items.Count > 0)
            {
                typeComboBox.SelectedIndex = 0;
            }
        }

        private void LoadDevices()
        {
            devicesListBox.Items.Clear();

            if (typeComboBox.SelectedItem == null)
                return;

            var selectedType = (ShellyDeviceType)typeComboBox.SelectedItem;

            foreach (var device in _plugin.Devices)
            {
                if (device.GetDeviceType() == selectedType)
                {
                    var item = new DeviceListItem
                    {
                        DeviceId = device.Id,
                        DisplayText = $"{device.Name} ({device.Id})"
                    };
                    devicesListBox.Items.Add(item);
                }
            }
        }

        private void TypeComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadDevices();
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(nameTextBox.Text))
            {
                MessageBox.Show("Please enter a group name.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (typeComboBox.SelectedItem == null)
            {
                MessageBox.Show("Please select a device type.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (devicesListBox.CheckedItems.Count == 0)
            {
                MessageBox.Show("Please select at least one device.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Update group
            _group.Name = nameTextBox.Text.Trim();
            _group.Type = (ShellyDeviceType)typeComboBox.SelectedItem;
            _group.DeviceIds.Clear();

            foreach (var item in devicesListBox.CheckedItems)
            {
                if (item is DeviceListItem deviceItem)
                {
                    _group.DeviceIds.Add(deviceItem.DeviceId);
                }
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private class DeviceListItem
        {
            public string DeviceId { get; set; }
            public string DisplayText { get; set; }

            public override string ToString()
            {
                return DisplayText;
            }
        }
    }
}
