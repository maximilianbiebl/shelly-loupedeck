using System;
using System.Collections.Generic;

namespace ShellyLoupedeckPlugin.Models
{
    /// <summary>
    /// Flexible folder with custom multi-level hierarchy
    /// </summary>
    public class FlexibleFolderConfiguration
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public FlexibleFolderLevel RootLevel { get; set; }

        public FlexibleFolderConfiguration()
        {
            Id = Guid.NewGuid().ToString();
            Name = "New Flexible Folder";
            RootLevel = new FlexibleFolderLevel
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Main Menu",
                Buttons = new List<FlexibleButton>()
            };
        }
    }

    /// <summary>
    /// Represents one level/page in the folder hierarchy
    /// </summary>
    public class FlexibleFolderLevel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<FlexibleButton> Buttons { get; set; }

        public FlexibleFolderLevel()
        {
            Id = Guid.NewGuid().ToString();
            Buttons = new List<FlexibleButton>();
        }
    }

    /// <summary>
    /// Represents a button that can either execute an action or navigate to a sublevel
    /// </summary>
    public class FlexibleButton
    {
        public string Label { get; set; }
        public FlexibleButtonType Type { get; set; }

        // For navigation buttons
        public FlexibleFolderLevel TargetLevel { get; set; }

        // For action buttons
        public string ActionType { get; set; } // e.g., "DeviceToggle", "Brightness", "Color", etc.
        public string DeviceId { get; set; }
        public string GroupId { get; set; }
        public string Parameter { get; set; }

        public FlexibleButton()
        {
        }

        // Constructor for navigation button
        public FlexibleButton(string label, FlexibleFolderLevel targetLevel)
        {
            Label = label;
            Type = FlexibleButtonType.Navigation;
            TargetLevel = targetLevel;
        }

        // Constructor for action button
        public FlexibleButton(string label, string actionType, string deviceId = null, string groupId = null, string parameter = null)
        {
            Label = label;
            Type = FlexibleButtonType.Action;
            ActionType = actionType;
            DeviceId = deviceId;
            GroupId = groupId;
            Parameter = parameter;
        }
    }

    public enum FlexibleButtonType
    {
        Navigation,  // Navigate to a sublevel
        Action       // Execute an action
    }
}
