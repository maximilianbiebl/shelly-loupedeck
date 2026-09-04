using System;
using System.Windows.Forms;

namespace ShellyLoupedeckPlugin.UI
{
    public class SettingsDialog : Form
    {
        private TextBox serverUrlTextBox;
        private TextBox authKeyTextBox;
        private Button saveButton;
        private Button cancelButton;
        private Label serverUrlLabel;
        private Label authKeyLabel;
        private Label titleLabel;
        private CheckBox verboseLoggingCheckBox;

        public string ServerUrl { get; set; }
        public string AuthKey { get; set; }
        public bool VerboseLogging { get; set; }
        public bool SaveClicked { get; private set; }

        public SettingsDialog(string currentServerUrl, string currentAuthKey, bool verboseLogging = false)
        {
            ServerUrl = currentServerUrl;
            AuthKey = currentAuthKey;
            VerboseLogging = verboseLogging;
            SaveClicked = false;

            InitializeComponents();
        }

        private void InitializeComponents()
        {
            // Form settings
            this.Text = "Shelly Cloud Settings";
            this.Width = 500;
            this.Height = 295;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            // Title
            titleLabel = new Label();
            titleLabel.Text = "Configure Shelly Cloud API";
            titleLabel.Font = new System.Drawing.Font("Segoe UI", 12, System.Drawing.FontStyle.Bold);
            titleLabel.Location = new System.Drawing.Point(20, 20);
            titleLabel.Size = new System.Drawing.Size(450, 30);
            this.Controls.Add(titleLabel);

            // Server URL Label
            serverUrlLabel = new Label();
            serverUrlLabel.Text = "Server URL:";
            serverUrlLabel.Location = new System.Drawing.Point(20, 60);
            serverUrlLabel.Size = new System.Drawing.Size(100, 20);
            this.Controls.Add(serverUrlLabel);

            // Server URL TextBox
            serverUrlTextBox = new TextBox();
            serverUrlTextBox.Location = new System.Drawing.Point(20, 85);
            serverUrlTextBox.Size = new System.Drawing.Size(440, 25);
            serverUrlTextBox.Text = ServerUrl;
            this.Controls.Add(serverUrlTextBox);

            // Auth Key Label
            authKeyLabel = new Label();
            authKeyLabel.Text = "Authorization Key:";
            authKeyLabel.Location = new System.Drawing.Point(20, 120);
            authKeyLabel.Size = new System.Drawing.Size(150, 20);
            this.Controls.Add(authKeyLabel);

            // Auth Key TextBox
            authKeyTextBox = new TextBox();
            authKeyTextBox.Location = new System.Drawing.Point(20, 145);
            authKeyTextBox.Size = new System.Drawing.Size(440, 25);
            authKeyTextBox.Text = AuthKey;
            authKeyTextBox.UseSystemPasswordChar = false; // Show the key for easier configuration
            this.Controls.Add(authKeyTextBox);

            // Verbose logging - off by default, since the device poll alone would
            // otherwise write several thousand lines an hour
            verboseLoggingCheckBox = new CheckBox();
            verboseLoggingCheckBox.Text = "Detailed logging (only for troubleshooting)";
            verboseLoggingCheckBox.Location = new System.Drawing.Point(20, 182);
            verboseLoggingCheckBox.Size = new System.Drawing.Size(320, 24);
            verboseLoggingCheckBox.Checked = VerboseLogging;
            this.Controls.Add(verboseLoggingCheckBox);

            // Save Button
            saveButton = new Button();
            saveButton.Text = "Save";
            saveButton.Location = new System.Drawing.Point(280, 215);
            saveButton.Size = new System.Drawing.Size(85, 30);
            saveButton.Click += SaveButton_Click;
            this.Controls.Add(saveButton);

            // Cancel Button
            cancelButton = new Button();
            cancelButton.Text = "Cancel";
            cancelButton.Location = new System.Drawing.Point(375, 215);
            cancelButton.Size = new System.Drawing.Size(85, 30);
            cancelButton.Click += CancelButton_Click;
            this.Controls.Add(cancelButton);
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            ServerUrl = serverUrlTextBox.Text.Trim();
            AuthKey = authKeyTextBox.Text.Trim();
            VerboseLogging = verboseLoggingCheckBox.Checked;
            SaveClicked = true;
            this.Close();
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            SaveClicked = false;
            this.Close();
        }
    }
}
