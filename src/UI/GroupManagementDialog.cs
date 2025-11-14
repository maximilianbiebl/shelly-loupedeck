using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using ShellyLoupedeckPlugin.Models;

namespace ShellyLoupedeckPlugin.UI
{
    public class GroupManagementDialog : Form
    {
        private ShellyLoupedeckPlugin _plugin;
        private ListBox groupListBox;
        private Button addButton;
        private Button editButton;
        private Button deleteButton;
        private Button closeButton;
        private Label titleLabel;

        public GroupManagementDialog(ShellyLoupedeckPlugin plugin)
        {
            _plugin = plugin;
            InitializeComponents();
            LoadGroups();
        }

        private void InitializeComponents()
        {
            // Form settings
            this.Text = "Manage Device Groups";
            this.Width = 600;
            this.Height = 500;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            // Title
            titleLabel = new Label();
            titleLabel.Text = "Device Groups";
            titleLabel.Font = new System.Drawing.Font("Segoe UI", 12, System.Drawing.FontStyle.Bold);
            titleLabel.Location = new System.Drawing.Point(20, 20);
            titleLabel.Size = new System.Drawing.Size(550, 30);
            this.Controls.Add(titleLabel);

            // Group ListBox
            groupListBox = new ListBox();
            groupListBox.Location = new System.Drawing.Point(20, 60);
            groupListBox.Size = new System.Drawing.Size(440, 350);
            groupListBox.DisplayMember = "DisplayText";
            groupListBox.SelectedIndexChanged += GroupListBox_SelectedIndexChanged;
            this.Controls.Add(groupListBox);

            // Add Button
            addButton = new Button();
            addButton.Text = "Add Group";
            addButton.Location = new System.Drawing.Point(470, 60);
            addButton.Size = new System.Drawing.Size(100, 30);
            addButton.Click += AddButton_Click;
            this.Controls.Add(addButton);

            // Edit Button
            editButton = new Button();
            editButton.Text = "Edit Group";
            editButton.Location = new System.Drawing.Point(470, 100);
            editButton.Size = new System.Drawing.Size(100, 30);
            editButton.Enabled = false;
            editButton.Click += EditButton_Click;
            this.Controls.Add(editButton);

            // Delete Button
            deleteButton = new Button();
            deleteButton.Text = "Delete Group";
            deleteButton.Location = new System.Drawing.Point(470, 140);
            deleteButton.Size = new System.Drawing.Size(100, 30);
            deleteButton.Enabled = false;
            deleteButton.Click += DeleteButton_Click;
            this.Controls.Add(deleteButton);

            // Close Button
            closeButton = new Button();
            closeButton.Text = "Close";
            closeButton.Location = new System.Drawing.Point(470, 420);
            closeButton.Size = new System.Drawing.Size(100, 30);
            closeButton.Click += (s, e) => this.Close();
            this.Controls.Add(closeButton);
        }

        private void LoadGroups()
        {
            groupListBox.Items.Clear();
            foreach (var group in _plugin.Groups)
            {
                var item = new GroupListItem
                {
                    Group = group,
                    DisplayText = $"{group.Name} ({group.Type}) - {group.DeviceIds.Count} device(s)"
                };
                groupListBox.Items.Add(item);
            }
        }

        private void GroupListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            bool hasSelection = groupListBox.SelectedIndex >= 0;
            editButton.Enabled = hasSelection;
            deleteButton.Enabled = hasSelection;
        }

        private void AddButton_Click(object sender, EventArgs e)
        {
            var dialog = new GroupEditDialog(_plugin, null);
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                _plugin.AddGroup(dialog.Group);
                LoadGroups();
            }
        }

        private void EditButton_Click(object sender, EventArgs e)
        {
            if (groupListBox.SelectedItem is GroupListItem item)
            {
                var dialog = new GroupEditDialog(_plugin, item.Group);
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    _plugin.UpdateGroup(dialog.Group);
                    LoadGroups();
                }
            }
        }

        private void DeleteButton_Click(object sender, EventArgs e)
        {
            if (groupListBox.SelectedItem is GroupListItem item)
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to delete the group '{item.Group.Name}'?",
                    "Delete Group",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );

                if (result == DialogResult.Yes)
                {
                    _plugin.RemoveGroup(item.Group.Id);
                    LoadGroups();
                }
            }
        }

        private class GroupListItem
        {
            public DeviceGroup Group { get; set; }
            public string DisplayText { get; set; }

            public override string ToString()
            {
                return DisplayText;
            }
        }
    }
}
