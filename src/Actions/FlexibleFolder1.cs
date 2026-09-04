namespace ShellyLoupedeckPlugin.Actions
{
    /// <summary>Flexible folder slot 1. Behaviour lives in <see cref="FlexibleFolderBase"/>.</summary>
    public class FlexibleFolder1 : FlexibleFolderBase
    {
        protected override int SlotIndex => 0;
        protected override string SlotName => "Flexible Folder 1";

        public FlexibleFolder1()
        {
            DisplayName = "Flexible Folder 1";
        }
    }
}
