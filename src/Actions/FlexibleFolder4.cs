namespace ShellyLoupedeckPlugin.Actions
{
    /// <summary>Flexible folder slot 4. Behaviour lives in <see cref="FlexibleFolderBase"/>.</summary>
    public class FlexibleFolder4 : FlexibleFolderBase
    {
        protected override int SlotIndex => 3;
        protected override string SlotName => "Flexible Folder 4";

        public FlexibleFolder4()
        {
            DisplayName = "Flexible Folder 4";
        }
    }
}
