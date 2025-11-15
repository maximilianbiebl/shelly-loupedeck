using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ShellyLoupedeckPlugin.Models;

namespace ShellyLoupedeckPlugin.UI
{
    public class FlexibleFolderEditorDialog : Form
    {
        private ShellyLoupedeckPlugin _plugin;
        private FlexibleFolderConfiguration _folder;
        private bool _isNewFolder;
        private FlexibleFolderLevel _selectedLevel;

        // UI Controls
        private TextBox _nameTextBox;
        private TreeView _levelTreeView;
        private ListBox _buttonsListBox;
        private Button _addLevelButton;
        private Button _renameLevelButton;
        private Button _deleteLevelButton;
        private Button _addDeviceButton;
        private Button _addNavigationButton;
        private Button _renameButtonButton;
        private Button _deleteButtonButton;
        private Button _moveUpButton;
        private Button _moveDownButton;
        private Button _saveButton;
        private Button _cancelButton;

        public FlexibleFolderEditorDialog(ShellyLoupedeckPlugin plugin, FlexibleFolderConfiguration folder)
        {
            _plugin = plugin;
            _folder = folder;
            _isNewFolder = folder.Id == null || !plugin.FlexibleFolders.Any(f => f.Id == folder.Id);
            _selectedLevel = folder.RootLevel;

            InitializeUI();
            LoadLevelTree();
            LoadButtons();
        }

        private void InitializeUI()
        {
            Text = _isNewFolder ? "New Flexible Folder" : "Edit Flexible Folder";
            Size = new Size(900, 600);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            // Folder Name
            var nameLabel = new Label
            {
                Text = "Folder Name:",
                Location = new Point(20, 20),
                Size = new Size(100, 20)
            };
            Controls.Add(nameLabel);

            _nameTextBox = new TextBox
            {
                Text = _folder.Name,
                Location = new Point(130, 18),
                Size = new Size(300, 25)
            };
            Controls.Add(_nameTextBox);

            // Level Tree (left side)
            var levelLabel = new Label
            {
                Text = "Folder Structure:",
                Location = new Point(20, 60),
                Size = new Size(250, 20),
                Font = new Font("Arial", 10, FontStyle.Bold)
            };
            Controls.Add(levelLabel);

            _levelTreeView = new TreeView
            {
                Location = new Point(20, 85),
                Size = new Size(250, 380),
                HideSelection = false
            };
            _levelTreeView.AfterSelect += LevelTreeView_AfterSelect;
            Controls.Add(_levelTreeView);

            // Level management buttons
            _addLevelButton = new Button
            {
                Text = "+ Level",
                Location = new Point(20, 475),
                Size = new Size(80, 30)
            };
            _addLevelButton.Click += AddLevelButton_Click;
            Controls.Add(_addLevelButton);

            _renameLevelButton = new Button
            {
                Text = "Rename",
                Location = new Point(110, 475),
                Size = new Size(80, 30)
            };
            _renameLevelButton.Click += RenameLevelButton_Click;
            Controls.Add(_renameLevelButton);

            _deleteLevelButton = new Button
            {
                Text = "Delete",
                Location = new Point(200, 475),
                Size = new Size(70, 30),
                Enabled = false // Root can't be deleted
            };
            _deleteLevelButton.Click += DeleteLevelButton_Click;
            Controls.Add(_deleteLevelButton);

            // Buttons List (right side)
            var buttonsLabel = new Label
            {
                Text = "Buttons in Selected Level:",
                Location = new Point(290, 60),
                Size = new Size(250, 20),
                Font = new Font("Arial", 10, FontStyle.Bold)
            };
            Controls.Add(buttonsLabel);

            _buttonsListBox = new ListBox
            {
                Location = new Point(290, 85),
                Size = new Size(580, 380)
            };
            _buttonsListBox.SelectedIndexChanged += (s, e) => UpdateButtonStates();
            Controls.Add(_buttonsListBox);

            // Button management
            _addDeviceButton = new Button
            {
                Text = "+ Device Action",
                Location = new Point(290, 475),
                Size = new Size(120, 30)
            };
            _addDeviceButton.Click += AddDeviceButton_Click;
            Controls.Add(_addDeviceButton);

            _addNavigationButton = new Button
            {
                Text = "+ Navigation",
                Location = new Point(420, 475),
                Size = new Size(120, 30)
            };
            _addNavigationButton.Click += AddNavigationButton_Click;
            Controls.Add(_addNavigationButton);

            _renameButtonButton = new Button
            {
                Text = "Rename",
                Location = new Point(550, 475),
                Size = new Size(80, 30),
                Enabled = false
            };
            _renameButtonButton.Click += RenameButtonButton_Click;
            Controls.Add(_renameButtonButton);

            _deleteButtonButton = new Button
            {
                Text = "Delete",
                Location = new Point(640, 475),
                Size = new Size(70, 30),
                Enabled = false
            };
            _deleteButtonButton.Click += DeleteButtonButton_Click;
            Controls.Add(_deleteButtonButton);

            _moveUpButton = new Button
            {
                Text = "↑",
                Location = new Point(720, 475),
                Size = new Size(40, 30),
                Enabled = false
            };
            _moveUpButton.Click += MoveUpButton_Click;
            Controls.Add(_moveUpButton);

            _moveDownButton = new Button
            {
                Text = "↓",
                Location = new Point(770, 475),
                Size = new Size(40, 30),
                Enabled = false
            };
            _moveDownButton.Click += MoveDownButton_Click;
            Controls.Add(_moveDownButton);

            // Save/Cancel
            _saveButton = new Button
            {
                Text = "Save",
                Location = new Point(690, 525),
                Size = new Size(90, 35),
                Font = new Font("Arial", 10, FontStyle.Bold)
            };
            _saveButton.Click += SaveButton_Click;
            Controls.Add(_saveButton);

            _cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(790, 525),
                Size = new Size(80, 35)
            };
            _cancelButton.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(_cancelButton);
        }

        private void LoadLevelTree()
        {
            _levelTreeView.Nodes.Clear();
            var rootNode = CreateTreeNode(_folder.RootLevel);
            _levelTreeView.Nodes.Add(rootNode);
            rootNode.Expand();
            _levelTreeView.SelectedNode = rootNode;
        }

        private TreeNode CreateTreeNode(FlexibleFolderLevel level)
        {
            var node = new TreeNode(level.Name) { Tag = level };

            // Add child levels (navigation targets)
            foreach (var button in level.Buttons)
            {
                if (button.Type == FlexibleButtonType.Navigation && button.TargetLevel != null)
                {
                    node.Nodes.Add(CreateTreeNode(button.TargetLevel));
                }
            }

            return node;
        }

        private void LevelTreeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node?.Tag is FlexibleFolderLevel level)
            {
                _selectedLevel = level;
                _deleteLevelButton.Enabled = (level != _folder.RootLevel);
                LoadButtons();
            }
        }

        private void LoadButtons()
        {
            _buttonsListBox.Items.Clear();

            if (_selectedLevel == null) return;

            foreach (var button in _selectedLevel.Buttons)
            {
                string display = "";
                if (button.Type == FlexibleButtonType.Navigation)
                {
                    display = $"[NAV] {button.Label ?? "→"} → {button.TargetLevel?.Name ?? "?"}";
                }
                else
                {
                    var actionDesc = button.ActionType ?? "Action";
                    var label = button.Label ?? "(no label)";
                    display = $"[{actionDesc}] {label}";
                }
                _buttonsListBox.Items.Add(display);
            }

            UpdateButtonStates();
        }

        private void UpdateButtonStates()
        {
            bool hasSelection = _buttonsListBox.SelectedIndex >= 0;
            bool canMoveUp = hasSelection && _buttonsListBox.SelectedIndex > 0;
            bool canMoveDown = hasSelection && _buttonsListBox.SelectedIndex < _selectedLevel.Buttons.Count - 1;

            _renameButtonButton.Enabled = hasSelection;
            _deleteButtonButton.Enabled = hasSelection;
            _moveUpButton.Enabled = canMoveUp;
            _moveDownButton.Enabled = canMoveDown;

            bool canAddMore = _selectedLevel.Buttons.Count < 8;
            _addDeviceButton.Enabled = canAddMore;
            _addNavigationButton.Enabled = canAddMore;
        }

        private void AddLevelButton_Click(object sender, EventArgs e)
        {
            string levelName = PromptForInput("New Level", "Enter level name:", "New Menu");
            if (string.IsNullOrWhiteSpace(levelName)) return;

            var newLevel = new FlexibleFolderLevel { Name = levelName.Trim() };

            // Add navigation button to current level
            if (_selectedLevel.Buttons.Count >= 8)
            {
                MessageBox.Show("Current level is full (max 8 buttons)", "Cannot Add", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var navButton = new FlexibleButton(levelName.Trim() + " →", newLevel);
            _selectedLevel.Buttons.Add(navButton);

            LoadLevelTree();
            LoadButtons();
        }

        private void RenameLevelButton_Click(object sender, EventArgs e)
        {
            if (_selectedLevel == null) return;

            string newName = PromptForInput("Rename Level", "Enter new name:", _selectedLevel.Name);
            if (string.IsNullOrWhiteSpace(newName)) return;

            _selectedLevel.Name = newName.Trim();
            LoadLevelTree();
        }

        private void DeleteLevelButton_Click(object sender, EventArgs e)
        {
            if (_selectedLevel == null || _selectedLevel == _folder.RootLevel) return;

            var result = MessageBox.Show(
                $"Delete level '{_selectedLevel.Name}' and all its buttons?",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes) return;

            // Find and remove navigation button pointing to this level
            RemoveLevelFromFolder(_folder.RootLevel, _selectedLevel);

            _selectedLevel = _folder.RootLevel;
            LoadLevelTree();
            LoadButtons();
        }

        private bool RemoveLevelFromFolder(FlexibleFolderLevel parent, FlexibleFolderLevel toRemove)
        {
            for (int i = parent.Buttons.Count - 1; i >= 0; i--)
            {
                var button = parent.Buttons[i];
                if (button.Type == FlexibleButtonType.Navigation)
                {
                    if (button.TargetLevel == toRemove)
                    {
                        parent.Buttons.RemoveAt(i);
                        return true;
                    }
                    else if (button.TargetLevel != null)
                    {
                        if (RemoveLevelFromFolder(button.TargetLevel, toRemove))
                            return true;
                    }
                }
            }
            return false;
        }

        private void AddDeviceButton_Click(object sender, EventArgs e)
        {
            if (_selectedLevel.Buttons.Count >= 8)
            {
                MessageBox.Show("Level is full (max 8 buttons)", "Cannot Add", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var dialog = new DeviceActionSelectorDialog(_plugin);
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                _selectedLevel.Buttons.Add(dialog.SelectedButton);
                LoadButtons();
            }
        }

        private void AddNavigationButton_Click(object sender, EventArgs e)
        {
            if (_selectedLevel.Buttons.Count >= 8)
            {
                MessageBox.Show("Level is full (max 8 buttons)", "Cannot Add", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            AddLevelButton_Click(sender, e); // Reuse add level logic
        }

        private void RenameButtonButton_Click(object sender, EventArgs e)
        {
            int index = _buttonsListBox.SelectedIndex;
            if (index < 0) return;

            var button = _selectedLevel.Buttons[index];
            string currentLabel = button.Label ?? "";

            string newLabel = PromptForInput("Rename Button", "Enter new label:", currentLabel);
            if (newLabel == null) return; // User cancelled

            button.Label = string.IsNullOrWhiteSpace(newLabel) ? null : newLabel.Trim();
            LoadButtons();
            _buttonsListBox.SelectedIndex = index;
        }

        private void DeleteButtonButton_Click(object sender, EventArgs e)
        {
            int index = _buttonsListBox.SelectedIndex;
            if (index < 0) return;

            _selectedLevel.Buttons.RemoveAt(index);
            LoadButtons();
        }

        private void MoveUpButton_Click(object sender, EventArgs e)
        {
            int index = _buttonsListBox.SelectedIndex;
            if (index <= 0) return;

            var button = _selectedLevel.Buttons[index];
            _selectedLevel.Buttons.RemoveAt(index);
            _selectedLevel.Buttons.Insert(index - 1, button);

            LoadButtons();
            _buttonsListBox.SelectedIndex = index - 1;
        }

        private void MoveDownButton_Click(object sender, EventArgs e)
        {
            int index = _buttonsListBox.SelectedIndex;
            if (index < 0 || index >= _selectedLevel.Buttons.Count - 1) return;

            var button = _selectedLevel.Buttons[index];
            _selectedLevel.Buttons.RemoveAt(index);
            _selectedLevel.Buttons.Insert(index + 1, button);

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
                _plugin.AddFlexibleFolder(_folder);
            }
            else
            {
                _plugin.UpdateFlexibleFolder(_folder);
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

            Label textLabel = new Label { Left = 20, Top = 20, Width = 350, Text = prompt };
            TextBox textBox = new TextBox { Left = 20, Top = 50, Width = 340, Text = defaultValue };
            Button okButton = new Button { Text = "OK", Left = 200, Width = 80, Top = 80, DialogResult = DialogResult.OK };
            Button cancelButton = new Button { Text = "Cancel", Left = 290, Width = 70, Top = 80, DialogResult = DialogResult.Cancel };

            okButton.Click += (s, e) => { inputForm.Close(); };
            cancelButton.Click += (s, e) => { inputForm.Close(); };

            inputForm.Controls.Add(textLabel);
            inputForm.Controls.Add(textBox);
            inputForm.Controls.Add(okButton);
            inputForm.Controls.Add(cancelButton);
            inputForm.AcceptButton = okButton;
            inputForm.CancelButton = cancelButton;

            return inputForm.ShowDialog() == DialogResult.OK ? textBox.Text : null;
        }
    }
}
