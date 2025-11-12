using Loupedeck;

namespace ShellyLoupedeckPlugin.Commands;

public class SettingsCommand : PluginDynamicCommand
{
    private ShellyLoupedeckPlugin _plugin = null!;

    public SettingsCommand()
    {
        DisplayName = "Shelly Settings";
        Description = "Configure Shelly Cloud API connection";
        GroupName = "Settings";
    }

    protected override bool OnLoad()
    {
        _plugin = (ShellyLoupedeckPlugin)Plugin;
        return base.OnLoad();
    }

    protected override void RunCommand(string actionParameter)
    {
        // This will open a simple dialog for settings
        // For now, users will need to configure settings via the plugin settings file
        // In a full implementation, you would show a custom UI dialog here
    }

    protected override BitmapImage? GetCommandImage(string actionParameter, PluginImageSize imageSize)
    {
        using var bitmapBuilder = new BitmapBuilder(imageSize);
        bitmapBuilder.Clear(BitmapColor.Black);
        bitmapBuilder.DrawText("Settings");
        return bitmapBuilder.ToImage();
    }
}
