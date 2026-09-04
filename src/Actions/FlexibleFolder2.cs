namespace ShellyLoupedeckPlugin.Actions
{
    /// <summary>Flexible folder slot 2. Behaviour lives in <see cref="FlexibleFolderBase"/>.</summary>
    public class FlexibleFolder2 : FlexibleFolderBase
    {
        protected override int SlotIndex => 1;
        protected override string SlotName => "Flexible Folder 2";

        public FlexibleFolder2()
        {
            DisplayName = "Flexible Folder 2";
        }
    }
}
