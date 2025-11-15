using Loupedeck;

namespace ShellyLoupedeckPlugin.Commands
{
    public class TestCommand : PluginDynamicCommand
    {
        public TestCommand()
        {
            DebugLogger.Log("!!! TEST COMMAND CONSTRUCTOR CALLED !!!");
            DisplayName = "TEST COMMAND";
            Description = "Test if commands load";
            GroupName = "Configuration";
        }

        protected override bool OnLoad()
        {
            DebugLogger.Log("!!! TEST COMMAND ONLOAD CALLED !!!");
            AddParameter("test", "Click Me!", "Test");
            return base.OnLoad();
        }

        protected override void RunCommand(string actionParameter)
        {
            DebugLogger.Log($"!!! TEST COMMAND RUN: {actionParameter} !!!");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var builder = new BitmapBuilder(imageSize))
            {
                builder.Clear(new BitmapColor(255, 0, 0)); // Red
                builder.DrawText("TEST", BitmapColor.White, 20);
                return builder.ToImage();
            }
        }
    }
}
