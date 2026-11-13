using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace TLDRevamp.VR.Native
{
    internal static class VrPerfTools
    {
        /// Measurement: every post-processing layer (Unity's post-processing stack, found by type name) on or off.
        internal static string PostEffects(bool on)
        {
            int n = 0;
            foreach (var c in Object.FindObjectsOfType<Behaviour>())
            {
                if (c == null || c.GetType().Name != "PostProcessLayer") continue;
                c.enabled = on;
                n++;
            }
            return "{\"postProcessLayers\":" + n + ",\"on\":" + (on ? "true" : "false") + "}";
        }

        /// Measurement: the post-processing layers (their anti-aliasing) and every effect in every volume's profile.
        internal static string PostList()
        {
            var b = new StringBuilder("{\"layers\":[");
            bool first = true;
            foreach (var c in Object.FindObjectsOfType<Behaviour>())
            {
                if (c == null || c.GetType().Name != "PostProcessLayer") continue;
                if (!first) b.Append(',');
                first = false;
                var aa = GameLookups.NamedField(typeof(UnityEngine.Component), "UnityEngine.Rendering.PostProcessing.PostProcessLayer:antialiasingMode", c.GetType())?.GetValue(c);
                var cam = c.GetComponent<Camera>();
                b.Append("{\"go\":").Append(Json.Str(c.gameObject.name)).Append(",\"camDepth\":").Append(cam != null ? VrNative.F(cam.depth) : "null")
                 .Append(",\"camOn\":").Append(cam != null && cam.isActiveAndEnabled ? "true" : "false").Append(",\"on\":").Append(c.isActiveAndEnabled ? "true" : "false")
                 .Append(",\"aa\":").Append(Json.Str(aa != null ? aa.ToString() : "?"))
                 .Append(",\"volumeLayer\":").Append(GameLookups.NamedField(typeof(UnityEngine.Component), "UnityEngine.Rendering.PostProcessing.PostProcessLayer:volumeLayer", c.GetType())?.GetValue(c) is LayerMask vm ? vm.value : -1).Append('}');
            }
            b.Append("],\"volumes\":[");
            first = true;
            foreach (var c in Object.FindObjectsOfType<Behaviour>(true))
            {
                if (c == null || c.GetType().Name != "PostProcessVolume") continue;
                if (!first) b.Append(',');
                first = false;
                var t = c.GetType();
                var prof = (GameLookups.NamedField(typeof(UnityEngine.Component), "UnityEngine.Rendering.PostProcessing.PostProcessVolume:sharedProfile", t)?.GetValue(c) as Object) ?? (GameLookups.NamedField(typeof(UnityEngine.Component), "UnityEngine.Rendering.PostProcessing.PostProcessVolume:profile", t)?.GetValue(c) as Object);
                b.Append("{\"go\":").Append(Json.Str(c.gameObject.name)).Append(",\"layer\":").Append(c.gameObject.layer)
                 .Append(",\"on\":").Append(c.isActiveAndEnabled ? "true" : "false")
                 .Append(",\"global\":").Append(GameLookups.NamedField(typeof(UnityEngine.Component), "UnityEngine.Rendering.PostProcessing.PostProcessVolume:isGlobal", t)?.GetValue(c) is bool g && g ? "true" : "false")
                 .Append(",\"weight\":").Append(GameLookups.NamedField(typeof(UnityEngine.Component), "UnityEngine.Rendering.PostProcessing.PostProcessVolume:weight", t)?.GetValue(c) is float w ? VrNative.F(w) : "null")
                 .Append(",\"priority\":").Append(GameLookups.NamedField(typeof(UnityEngine.Component), "UnityEngine.Rendering.PostProcessing.PostProcessVolume:priority", t)?.GetValue(c) is float pr ? VrNative.F(pr) : "null")
                 .Append(",\"profile\":").Append(Json.Str(prof != null ? prof.name : "")).Append('}');
            }
            b.Append("],\"effects\":[");
            first = true;
            foreach (var e in Effects())
            {
                if (!first) b.Append(',');
                first = false;
                b.Append("{\"profile\":").Append(Json.Str(e.Profile)).Append(",\"type\":").Append(Json.Str(e.Setting.GetType().Name))
                 .Append(",\"on\":").Append(Enabled(e.Setting) ? "true" : "false").Append('}');
            }
            return b.Append("]}").ToString();
        }

        /// Measurement: the enabled cameras that draw into the headset and the layers each draws.
        internal static string CullList()
        {
            var b = new StringBuilder("{\"cameras\":[");
            bool first = true;
            foreach (var cam in Camera.allCameras)
            {
                if (cam == null || cam.targetTexture != null || cam.stereoTargetEye == StereoTargetEyeMask.None) continue;
                if (!first) b.Append(',');
                first = false;
                b.Append("{\"name\":").Append(Json.Str(cam.name)).Append(",\"depth\":").Append(VrNative.F(cam.depth))
                 .Append(",\"far\":").Append(VrNative.F(cam.farClipPlane)).Append(",\"mask\":").Append(cam.cullingMask).Append(",\"layers\":[");
                bool f2 = true;
                for (int i = 0; i < 32; i++)
                {
                    if ((cam.cullingMask & (1 << i)) == 0) continue;
                    if (!f2) b.Append(',');
                    f2 = false;
                    float d = cam.layerCullDistances != null && cam.layerCullDistances.Length == 32 ? cam.layerCullDistances[i] : 0f;
                    b.Append("{\"i\":").Append(i).Append(",\"name\":").Append(Json.Str(LayerMask.LayerToName(i))).Append(",\"cull\":").Append(VrNative.F(d)).Append('}');
                }
                b.Append("]}");
            }
            return b.Append("]}").ToString();
        }

        /// Measurement: what the headset cameras draw now, grouped by shader (renderers the cameras saw last frame),
        /// with the renderer count, the material passes and the share that sits behind the camera's view.
        internal static string Visible(int top)
        {
            var groups = new Dictionary<string, int[]>();
            var cam = VrNative.Player != null ? VrNative.Player.mainCam : null;
            foreach (var r in Object.FindObjectsOfType<Renderer>())
            {
                if (r == null || !r.isVisible || r.gameObject.layer == 31) continue;
                foreach (var m in r.sharedMaterials)
                {
                    string key = m != null && m.shader != null ? m.shader.name : "(none)";
                    if (!groups.TryGetValue(key, out var g)) groups[key] = g = new int[3];
                    g[0]++;
                    g[1] += m != null ? m.passCount : 0;
                    if (cam != null && r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off) g[2]++;
                }
            }
            var b = new StringBuilder("{\"shaders\":[");
            bool first = true;
            var list = new List<KeyValuePair<string, int[]>>(groups);
            list.Sort((x, y) => y.Value[0].CompareTo(x.Value[0]));
            for (int i = 0; i < list.Count && i < top; i++)
            {
                if (!first) b.Append(',');
                first = false;
                b.Append("{\"shader\":").Append(Json.Str(list[i].Key)).Append(",\"materials\":").Append(list[i].Value[0])
                 .Append(",\"passes\":").Append(list[i].Value[1]).Append(",\"casters\":").Append(list[i].Value[2]).Append('}');
            }
            return b.Append("],\"groups\":").Append(groups.Count).Append('}').ToString();
        }

        /// Measurement: every renderer with a material whose shader name holds `part` is hidden, or shown again.
        internal static string Hide(string part, bool hide)
        {
            int n = 0;
            if (!hide)
            {
                foreach (var r in _hidden) if (r != null) { r.forceRenderingOff = false; n++; }
                _hidden.Clear();
                return "{\"shown\":" + n + "}";
            }
            foreach (var r in Object.FindObjectsOfType<Renderer>())
            {
                if (r == null || r.forceRenderingOff) continue;
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null || m.shader == null || m.shader.name.IndexOf(part, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                    r.forceRenderingOff = true; _hidden.Add(r); n++;
                    break;
                }
            }
            return "{\"hidden\":" + n + "}";
        }

        private static readonly List<Renderer> _hidden = new List<Renderer>();

        /// Measurement: one layer off or on for the headset cameras with that name ("all": every one).
        internal static string Cull(string camName, int layer, bool on)
        {
            int n = 0;
            foreach (var cam in Camera.allCameras)
            {
                if (cam == null || cam.targetTexture != null || (camName != "all" && cam.name != camName)) continue;
                if (!_cullSaved.ContainsKey(cam)) _cullSaved[cam] = cam.cullingMask;
                // back on only where the camera drew that layer in the first place
                if (on) cam.cullingMask |= _cullSaved[cam] & (1 << layer); else cam.cullingMask &= ~(1 << layer);
                n++;
            }
            return "{\"cameras\":" + n + "}";
        }

        /// Measurement: every camera a cull switch touched draws its layers as before.
        internal static string CullRestore()
        {
            int n = 0;
            foreach (var kv in _cullSaved) if (kv.Key != null) { kv.Key.cullingMask = kv.Value; n++; }
            _cullSaved.Clear();
            return "{\"restored\":" + n + "}";
        }

        private static readonly Dictionary<Camera, int> _cullSaved = new Dictionary<Camera, int>();

        /// Measurement: one effect type on or off in every profile.
        internal static string PostEffect(string type, bool on)
        {
            int n = 0;
            foreach (var e in Effects())
            {
                if (e.Setting.GetType().Name != type) continue;
                var en = GameLookups.NamedField(typeof(UnityEngine.Component), "UnityEngine.Rendering.PostProcessing.PostProcessEffectSettings:enabled", e.Setting.GetType())?.GetValue(e.Setting);
                GameLookups.NamedField(typeof(UnityEngine.Component), "UnityEngine.Rendering.PostProcessing.BoolParameter:value", en?.GetType())?.SetValue(en, on);
                n++;
            }
            return "{\"type\":" + Json.Str(type) + ",\"changed\":" + n + "}";
        }

        /// Measurement: one post-processing layer (by its index in `post list`) on/off, or its anti-aliasing mode.
        internal static string PostLayer(int index, string what)
        {
            int i = 0;
            foreach (var c in Object.FindObjectsOfType<Behaviour>())
            {
                if (c == null || c.GetType().Name != "PostProcessLayer") continue;
                if (i++ != index) continue;
                if (what == "on" || what == "off") c.enabled = what == "on";
                else
                {
                    var f = GameLookups.NamedField(typeof(UnityEngine.Component), "UnityEngine.Rendering.PostProcessing.PostProcessLayer:antialiasingMode", c.GetType());
                    if (f != null) f.SetValue(c, System.Enum.Parse(f.FieldType, what));
                }
                return "{\"layer\":" + index + ",\"go\":" + Json.Str(c.gameObject.name) + ",\"set\":" + Json.Str(what) + "}";
            }
            return "{\"error\":\"no layer " + index + "\"}";
        }

        private struct Effect { public string Profile; public object Setting; }

        private static IEnumerable<Effect> Effects()
        {
            var seen = new HashSet<int>();
            foreach (var c in Object.FindObjectsOfType<Behaviour>())
            {
                if (c == null || c.GetType().Name != "PostProcessVolume") continue;
                foreach (var f in new[] { "sharedProfile", "profile" })
                {
                    var prof = (f == "sharedProfile"
                        ? GameLookups.NamedField(typeof(UnityEngine.Component), "UnityEngine.Rendering.PostProcessing.PostProcessVolume:sharedProfile", c.GetType())
                        : GameLookups.NamedField(typeof(UnityEngine.Component), "UnityEngine.Rendering.PostProcessing.PostProcessVolume:profile", c.GetType()))?.GetValue(c) as Object;
                    if (prof == null || !seen.Add(prof.GetInstanceID())) continue;
                    if (!(GameLookups.NamedField(typeof(UnityEngine.Component), "UnityEngine.Rendering.PostProcessing.PostProcessProfile:settings", prof.GetType())?.GetValue(prof) is System.Collections.IList list)) continue;
                    foreach (var st in list) if (st != null) yield return new Effect { Profile = prof.name, Setting = st };
                }
            }
        }

        private static bool Enabled(object setting)
        {
            var en = GameLookups.NamedField(typeof(UnityEngine.Component), "UnityEngine.Rendering.PostProcessing.PostProcessEffectSettings:enabled", setting.GetType())?.GetValue(setting);
            return GameLookups.NamedField(typeof(UnityEngine.Component), "UnityEngine.Rendering.PostProcessing.BoolParameter:value", en?.GetType())?.GetValue(en) is bool v && v;
        }

    }
}
