namespace ShellyLoupedeckPlugin.Actions
{
    /// <summary>Flexible folder slot 9. Behaviour lives in <see cref="FlexibleFolderBase"/>.</summary>
    public class FlexibleFolder9 : FlexibleFolderBase
    {
        protected override int SlotIndex => 8;
        protected override string SlotName => "Flexible Folder 9";

        public FlexibleFolder9()
        {
            DisplayName = "Flexible Folder 9";
        }
    }
}
