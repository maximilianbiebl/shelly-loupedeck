namespace ShellyLoupedeckPlugin.Actions
{
    /// <summary>Flexible folder slot 3. Behaviour lives in <see cref="FlexibleFolderBase"/>.</summary>
    public class FlexibleFolder3 : FlexibleFolderBase
    {
        protected override int SlotIndex => 2;
        protected override string SlotName => "Flexible Folder 3";

        public FlexibleFolder3()
        {
            DisplayName = "Flexible Folder 3";
        }
    }
}
