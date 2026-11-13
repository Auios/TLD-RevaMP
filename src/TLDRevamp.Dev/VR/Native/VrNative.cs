using System;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.XR;
using static TLDRevamp.VR.Native.VrNative;

namespace TLDRevamp.VR.Native
{
    internal static class VrNativeTools
    {

        // ---- the debug bridge: `vrnative` (read) and `vrnative sim ...` (a test drives the hands)

        internal static string Command(string arg)
        {
            arg = (arg ?? "").Trim();
            if (arg.Length == 0 || arg == "state") return Json();
            if (arg.StartsWith("sim", StringComparison.Ordinal)) return VrSim.Command(arg.Substring(3).Trim());
            if (arg == "body" && DevHooks.VrBody != null) return DevHooks.VrBody();
            if (arg.StartsWith("items", StringComparison.Ordinal)) return TLDRevamp.VR.Native.VrHandsTools.ItemsJson(arg.Length > 5 ? float.Parse(arg.Substring(5).Trim(), CultureInfo.InvariantCulture) : 6f);
            if (arg == "comfort on" || arg == "comfort off") { Comfort = arg.EndsWith("on"); return Json(); }
            if (arg == "hands on" || arg == "hands off") { Hands = arg.EndsWith("on"); return Json(); }
            if (arg == "height on" || arg == "height off") { VrHeight.Enabled = arg.EndsWith("on"); return Json(); }
            if (arg == "mirror on" || arg == "mirror off") { VrPerf.DesktopMirror = arg.EndsWith("on"); return Json(); }
            if (arg == "sharedmirrors on" || arg == "sharedmirrors off") { VrPerf.SharedMirrors = arg.EndsWith("on"); return Json(); }
            if (arg == "post on" || arg == "post off") return VrPerfTools.PostEffects(arg.EndsWith("on"));
            if (arg == "post list") return VrPerfTools.PostList();
            if (arg == "singlepass on" || arg == "singlepass off") { VrPerf.SinglePass = arg.EndsWith("on"); return Json(); }
            if (arg == "cost")
            {
                var cb = new StringBuilder("{\"frames\":").Append(VrNativeRunner.CostFrames);
                for (int i = 0; i < VrNativeRunner.CostMs.Length; i++)
                {
                    cb.Append(",\"").Append(VrNativeRunner.CostNames[i]).Append("Ms\":").Append(F((float)(VrNativeRunner.CostMs[i] / Math.Max(1, VrNativeRunner.CostFrames))));
                    VrNativeRunner.CostMs[i] = 0;
                }
                VrNativeRunner.CostFrames = 0;
                return cb.Append('}').ToString();
            }
            if (arg.StartsWith("sky faces ", StringComparison.Ordinal)) { VrSky.FacesPerFrame = int.Parse(arg.Substring(10).Trim(), CultureInfo.InvariantCulture); return Json(); }
            if (arg == "visible" || arg.StartsWith("visible ", StringComparison.Ordinal)) return VrPerfTools.Visible(arg.Length > 8 ? int.Parse(arg.Substring(8).Trim(), CultureInfo.InvariantCulture) : 25);
            if (arg == "show") return VrPerfTools.Hide("", false);
            if (arg.StartsWith("hide ", StringComparison.Ordinal)) return VrPerfTools.Hide(arg.Substring(5).Trim(), true);
            if (arg == "cull list") return VrPerfTools.CullList();
            if (arg == "cull restore") return VrPerfTools.CullRestore();
            if (arg.StartsWith("cull ", StringComparison.Ordinal))
            {
                // cull <camera name|all> <layer> on|off  (a camera name may hold spaces: the last two words are the layer and the switch)
                var w = arg.Substring(5).Trim().Split(' ');
                if (w.Length >= 3) return VrPerfTools.Cull(string.Join(" ", w, 0, w.Length - 2), int.Parse(w[w.Length - 2], CultureInfo.InvariantCulture), w[w.Length - 1] == "on");
            }
            if (arg == "taa on" || arg == "taa off") { VrPerf.NoTaa = arg.EndsWith("off"); return Json(); }
            if (arg.StartsWith("post layer ", StringComparison.Ordinal))
            {
                var pa = arg.Substring(11).Trim().Split(' ');
                return VrPerfTools.PostLayer(int.Parse(pa[0], CultureInfo.InvariantCulture), pa.Length > 1 ? pa[1] : "on");
            }
            if (arg.StartsWith("post fx ", StringComparison.Ordinal))
            {
                var pa = arg.Substring(8).Trim().Split(' ');
                return VrPerfTools.PostEffect(pa[0], pa.Length > 1 && pa[1] == "on");
            }
            if (arg.StartsWith("msaa ", StringComparison.Ordinal)) { VrPerf.MaxMsaa = int.Parse(arg.Substring(5).Trim(), CultureInfo.InvariantCulture); return Json(); }
            if (arg.StartsWith("scale ", StringComparison.Ordinal)) { VrPerf.RenderScale = float.Parse(arg.Substring(6).Trim(), CultureInfo.InvariantCulture); return Json(); }
            return "{\"error\":\"usage: vrnative [state] | body | items [radius] | comfort on|off | hands on|off | height on|off | mirror on|off | sharedmirrors on|off | scale 0.5..1 | msaa 0|2|4|8 | singlepass on|off | taa on|off | visible [n] | hide <shader part> | show | cost | sky faces 1..6 | cull list | cull restore | cull <camera|all> <layer> on|off | sim on|off | sim <left|right> at x y z [dir dx dy dz] | sim <left|right> <grip|trigger|primary> <down|up> | sim <left|right> to x y z\"}";
        }

        internal static string Json()
        {
            var b = new StringBuilder(512);
            b.Append("{\"active\":").Append(Active ? "true" : "false");
            b.Append(",\"headset\":").Append(Headset ? "true" : "false");
            b.Append(",\"gameVr\":").Append(GameVr() ? "true" : "false");
            b.Append(",\"comfort\":").Append(Comfort ? "true" : "false");
            b.Append(",\"hands\":").Append(Hands ? "true" : "false");
            b.Append(",\"menu\":").Append(MenuOpen ? "true" : "false");
            b.Append(",\"sim\":").Append((DevHooks.VrSimulation?.Invoke() ?? false) ? "true" : "false");
            b.Append(",\"comfortApplied\":").Append(VrComfort.AppliedFrames);
            b.Append(',');
            VrComfort.AppendJson(b);
            b.Append(',');
            VrHands.AppendJson(b);
            b.Append(',');
            VrPerf.AppendJson(b);
            b.Append(',');
            VrHeight.AppendJson(b);
            b.Append(',');
            VrSky.AppendJson(b);
            b.Append('}');
            return b.ToString();
        }
    }
}
