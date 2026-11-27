namespace TLDRevamp
{
    /// The test build's settings window (the game's is ModHost.Settings.cs, Unity's IMGUI): the mod's Config() only, as
    /// the window calls it.
    public static partial class ModHost
    {
        static partial void DrawConfigWindow(Loaded l) => Call(l, l.Config, "Config");
        static partial void ConfigWindowReset() { }
    }
}
