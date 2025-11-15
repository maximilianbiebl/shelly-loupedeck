using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ShellyLoupedeckPlugin.Models;

namespace ShellyLoupedeckPlugin.UI
{
    public class DeviceActionSelectorDialog : Form
    {
        private ShellyLoupedeckPlugin _plugin;
        private ComboBox _deviceComboBox;
        private ComboBox _actionTypeComboBox;
        private TextBox _labelTextBox;
        private Button _okButton;
        private Button _cancelButton;

        public FlexibleButton SelectedButton { get; private set; }

        public DeviceActionSelectorDialog(ShellyLoupedeckPlugin plugin)
        {
            _plugin = plugin;
            InitializeUI();
            LoadDevices();
        }

        private void InitializeUI()
        {
            Text = "Add Device Action";
            Size = new Size(450, 220);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            // Device selection
            var deviceLabel = new Label
            {
                Text = "Select Device:",
                Location = new Point(20, 20),
                Size = new Size(100, 20)
            };
            Controls.Add(deviceLabel);

            _deviceComboBox = new ComboBox
            {
                Location = new Point(130, 18),
                Size = new Size(280, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            Controls.Add(_deviceComboBox);

            // Action type selection
            var actionLabel = new Label
            {
                Text = "Action Type:",
                Location = new Point(20, 60),
                Size = new Size(100, 20)
            };
            Controls.Add(actionLabel);

            _actionTypeComboBox = new ComboBox
            {
                Location = new Point(130, 58),
                Size = new Size(280, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _actionTypeComboBox.Items.AddRange(new object[]
            {
                "DeviceToggle",
                "Brightness",
                "Dimmer",
                "Color",
                "Temperature"
            });
            _actionTypeComboBox.SelectedIndex = 0;
            Controls.Add(_actionTypeComboBox);

            // Label input
            var labelLabel = new Label
            {
                Text = "Button Label:",
                Location = new Point(20, 100),
                Size = new Size(100, 20)
            };
            Controls.Add(labelLabel);

            _labelTextBox = new TextBox
            {
                Location = new Point(130, 98),
                Size = new Size(280, 25)
            };
            Controls.Add(_labelTextBox);

            var labelHintLabel = new Label
            {
                Text = "(optional - leave empty for no label)",
                Location = new Point(130, 128),
                Size = new Size(280, 20),
                ForeColor = Color.Gray,
                Font = new Font("Arial", 8)
            };
            Controls.Add(labelHintLabel);

            // OK/Cancel buttons
            _okButton = new Button
            {
                Text = "Add",
                Location = new Point(250, 140),
                Size = new Size(80, 30)
            };
            _okButton.Click += OkButton_Click;
            Controls.Add(_okButton);

            _cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(340, 140),
                Size = new Size(70, 30)
            };
            _cancelButton.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(_cancelButton);

            AcceptButton = _okButton;
            CancelButton = _cancelButton;
        }

        private void LoadDevices()
        {
            _deviceComboBox.DisplayMember = "Name";
            _deviceComboBox.ValueMember = "Id";

            foreach (var device in _plugin.Devices)
            {
                _deviceComboBox.Items.Add(device);
            }

            if (_deviceComboBox.Items.Count > 0)
                _deviceComboBox.SelectedIndex = 0;
        }

        private void OkButton_Click(object sender, EventArgs e)
        {
            if (_deviceComboBox.SelectedItem == null)
            {
                MessageBox.Show("Please select a device.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var device = (ShellyDevice)_deviceComboBox.SelectedItem;
            var actionType = _actionTypeComboBox.SelectedItem?.ToString() ?? "DeviceToggle";
            var label = string.IsNullOrWhiteSpace(_labelTextBox.Text) ? null : _labelTextBox.Text.Trim();

            SelectedButton = new FlexibleButton(
                label,
                actionType,
                deviceId: device.Id
            );

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
