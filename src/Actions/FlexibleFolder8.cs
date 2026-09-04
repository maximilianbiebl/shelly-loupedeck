namespace ShellyLoupedeckPlugin.Actions
{
    /// <summary>Flexible folder slot 8. Behaviour lives in <see cref="FlexibleFolderBase"/>.</summary>
    public class FlexibleFolder8 : FlexibleFolderBase
    {
        protected override int SlotIndex => 7;
        protected override string SlotName => "Flexible Folder 8";

        public FlexibleFolder8()
        {
            DisplayName = "Flexible Folder 8";
        }
    }
}
