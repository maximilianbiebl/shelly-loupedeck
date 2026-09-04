namespace ShellyLoupedeckPlugin.Actions
{
    /// <summary>Flexible folder slot 6. Behaviour lives in <see cref="FlexibleFolderBase"/>.</summary>
    public class FlexibleFolder6 : FlexibleFolderBase
    {
        protected override int SlotIndex => 5;
        protected override string SlotName => "Flexible Folder 6";

        public FlexibleFolder6()
        {
            DisplayName = "Flexible Folder 6";
        }
    }
}
