using System;
using System.Collections.Generic;

namespace ShellyLoupedeckPlugin.Models
{
    /// <summary>
    /// Represents a configured touchfield folder with custom button selection
    /// </summary>
    public class FolderConfiguration
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<FolderButton> Buttons { get; set; }

        public FolderConfiguration()
        {
            Id = Guid.NewGuid().ToString();
            Name = "New Folder";
            Buttons = new List<FolderButton>();
        }
    }

    /// <summary>
    /// Represents a button within a folder
    /// </summary>
    public class FolderButton
    {
        public FolderButtonType Type { get; set; }
        public string TargetId { get; set; } // Device ID or Group ID
        public string Parameter { get; set; } // e.g., "red" for color, "50" for brightness, "ch0" for channel
        public string CustomLabel { get; set; } // Optional custom label

        // For GenericAction type
        public string ActionName { get; set; } // Full name of the action/command class
        public string ActionParameter { get; set; } // Parameter for the action

        public FolderButton()
        {
        }

        public FolderButton(FolderButtonType type, string targetId, string parameter = null, string customLabel = null)
        {
            Type = type;
            TargetId = targetId;
            Parameter = parameter;
            CustomLabel = customLabel;
        }

        // Constructor for generic actions
        public FolderButton(string actionName, string actionParameter, string customLabel)
        {
            Type = FolderButtonType.GenericAction;
            ActionName = actionName;
            ActionParameter = actionParameter;
            CustomLabel = customLabel;
        }
    }

    public enum FolderButtonType
    {
        DeviceToggle,      // Toggle a specific device on/off
        GroupColor,        // Set color for a group
        GroupBrightness,   // Set brightness for a group
        GroupTemperature,  // Set temperature for thermostat group
        GroupToggle,       // Toggle all devices in a group
        GenericAction      // Any plugin action
    }
}
