namespace TLDRevamp.Net
{
    internal static class EntityGround
    {
        public static string GroundLayers = "sand,sand2,sand3,road";
    }

    public static partial class Entities
    {
        public static string GroundLayers { get => EntityGround.GroundLayers; set => EntityGround.GroundLayers = value; }
    }
}
