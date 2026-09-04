namespace ShellyLoupedeckPlugin.Actions
{
    /// <summary>Flexible folder slot 7. Behaviour lives in <see cref="FlexibleFolderBase"/>.</summary>
    public class FlexibleFolder7 : FlexibleFolderBase
    {
        protected override int SlotIndex => 6;
        protected override string SlotName => "Flexible Folder 7";

        public FlexibleFolder7()
        {
            DisplayName = "Flexible Folder 7";
        }
    }
}
