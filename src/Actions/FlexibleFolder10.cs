namespace ShellyLoupedeckPlugin.Actions
{
    /// <summary>Flexible folder slot 10. Behaviour lives in <see cref="FlexibleFolderBase"/>.</summary>
    public class FlexibleFolder10 : FlexibleFolderBase
    {
        protected override int SlotIndex => 9;
        protected override string SlotName => "Flexible Folder 10";

        public FlexibleFolder10()
        {
            DisplayName = "Flexible Folder 10";
        }
    }
}
