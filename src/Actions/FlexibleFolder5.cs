namespace ShellyLoupedeckPlugin.Actions
{
    /// <summary>Flexible folder slot 5. Behaviour lives in <see cref="FlexibleFolderBase"/>.</summary>
    public class FlexibleFolder5 : FlexibleFolderBase
    {
        protected override int SlotIndex => 4;
        protected override string SlotName => "Flexible Folder 5";

        public FlexibleFolder5()
        {
            DisplayName = "Flexible Folder 5";
        }
    }
}
