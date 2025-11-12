using Loupedeck;

namespace ShellyLoupedeckPlugin
{
    // ClientApplication for Shelly Cloud Control
    // This is a universal plugin that works without a specific application
    public class ShellyClientApplication : ClientApplication
    {
        public ShellyClientApplication()
        {
        }

        protected override string GetProcessName()
        {
            // Return empty string - this is a universal plugin
            return "";
        }

        protected override string GetBundleName()
        {
            // Return empty string - this is a universal plugin
            return "";
        }
    }
}
