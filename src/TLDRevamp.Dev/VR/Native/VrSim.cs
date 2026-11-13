using System;
using System.Globalization;
using UnityEngine;

namespace TLDRevamp.VR.Native
{
    /// The hands without a headset, for tests: a test places a hand in the world and presses its buttons through the
    /// debug bridge, and everything downstream (grab, hold, throw, use, the interaction ray) runs as it does with real
    /// controllers. With the headset on, a simulated hand replaces only that hand.
    internal static class VrSim
    {
        internal static bool On;
        internal static readonly bool[] Hand = new bool[2];
        internal static readonly Vector3[] Pos = new Vector3[2];
        internal static readonly Quaternion[] Rot = { Quaternion.identity, Quaternion.identity };
        internal static readonly bool[] Trigger = new bool[2], Grip = new bool[2], Primary = new bool[2];
        // a hand moved by a test glides to its target over a few frames, so the throw sees a real speed
        internal static readonly Vector3[] Target = new Vector3[2];
        internal static readonly float[] GlideS = new float[2];

        internal static void Tick()
        {
            if (!On) return;
            for (int h = 0; h < 2; h++)
            {
                if (!Hand[h] || GlideS[h] <= 0f) continue;
                float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);
                float k = Mathf.Clamp01(dt / GlideS[h]);
                Pos[h] = Vector3.Lerp(Pos[h], Target[h], k);
                GlideS[h] -= dt;
                if (GlideS[h] <= 0f) Pos[h] = Target[h];
            }
        }

        internal static string Command(string arg)
        {
            var a = arg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (a.Length == 0) return TLDRevamp.VR.Native.VrNativeTools.Json();
            if (a[0] == "on") { On = true; return TLDRevamp.VR.Native.VrNativeTools.Json(); }
            if (a[0] == "off")
            {
                On = false;
                for (int i = 0; i < 2; i++) { Hand[i] = false; Trigger[i] = Grip[i] = Primary[i] = false; GlideS[i] = 0f; }
                return TLDRevamp.VR.Native.VrNativeTools.Json();
            }
            int h = a[0] == "left" ? 0 : a[0] == "right" ? 1 : -1;
            if (h < 0 || a.Length < 2) return TLDRevamp.VR.Native.VrNativeTools.Command("?");
            var ci = CultureInfo.InvariantCulture;
            if ((a[1] == "at" || a[1] == "to") && a.Length >= 5)
            {
                var p = new Vector3(float.Parse(a[2], ci), float.Parse(a[3], ci), float.Parse(a[4], ci));
                if (a.Length >= 9 && a[5] == "dir")
                {
                    var d = new Vector3(float.Parse(a[6], ci), float.Parse(a[7], ci), float.Parse(a[8], ci));
                    if (d.sqrMagnitude > 1e-6f) Rot[h] = Quaternion.LookRotation(d.normalized, Vector3.up);
                }
                Hand[h] = true;
                On = true;
                if (a[1] == "at") { Pos[h] = p; Target[h] = p; GlideS[h] = 0f; }
                else
                {
                    float s = 0.12f;
                    if (a.Length >= 7 && a[a.Length - 2] == "in") s = float.Parse(a[a.Length - 1], ci);
                    Target[h] = p; GlideS[h] = Mathf.Max(0.02f, s);
                }
                return TLDRevamp.VR.Native.VrNativeTools.Json();
            }
            if (a.Length >= 3 && (a[2] == "down" || a[2] == "up"))
            {
                bool v = a[2] == "down";
                if (a[1] == "grip") Grip[h] = v;
                else if (a[1] == "trigger") Trigger[h] = v;
                else if (a[1] == "primary") Primary[h] = v;
                else return TLDRevamp.VR.Native.VrNativeTools.Command("?");
                Hand[h] = true;
                On = true;
                return TLDRevamp.VR.Native.VrNativeTools.Json();
            }
            return TLDRevamp.VR.Native.VrNativeTools.Command("?");
        }
    }

}
