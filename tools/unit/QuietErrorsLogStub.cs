// Minimal game logger for isolated source harnesses which previously had no logging dependency.
namespace TLDRevamp
{
    internal static class Plugin
    {
        internal static readonly QuietErrorsLog Log = new QuietErrorsLog();
        internal sealed class QuietErrorsLog { internal void LogWarning(string message) { } }
    }
}
