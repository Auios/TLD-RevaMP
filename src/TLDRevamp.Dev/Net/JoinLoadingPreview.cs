using System.Linq;
using static TLDRevamp.Net.JoinLoading;

namespace TLDRevamp.Net
{
    internal static class JoinLoadingTools
    {
        internal static string PreviewFromBridge(string[] args)
        {
            JoinLoadingPreview.Install();
            if (!DevHooks.Testing) return "{\"error\":\"Preview requires the test bridge\"}";
            if (Visible) return "{\"error\":\"Cannot preview during a join\"}";
            if (!JoinLoadingPreview.Command(args)) return "{\"error\":\"mp loading preview <0-6> [question|stuck] [expanded] | off\"}";
            ResetPresentation(JoinLoadingPreview.Expanded);
            return "{\"preview\":" + (JoinLoadingPreview.Active ? "true" : "false") + ",\"sprites\":" + JoinLoadingSkin.SpriteInfo() + "}";
        }
        internal static string ToggleFromBridge(string[] args)
        {
            if (!DevHooks.Testing) return "{\"error\":\"Toggle requires the test bridge\"}";
            bool toggled = args.Length == 3 && args[2] == "received" ? ToggleReceived() :
                args.Length == 4 && args[2] == "step" && int.TryParse(args[3], out int step) && ToggleStep(step);
            return toggled ? Probe() : "{\"error\":\"mp loading toggle step <0-6> | received (requires a visible disclosure)\"}";
        }
    }
}
