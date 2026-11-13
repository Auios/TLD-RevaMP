using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using static TLDRevamp.Fixes.FastNoise;

namespace TLDRevamp.Fixes
{
    internal static class FastNoiseTools
    {

        /// Bit-exact comparison against the game's NoiseS3D on n random points (2D and 3D), plus timing.
        public static string Check(int n)
        {
            if (Get() == null) NoiseS3D.Noise(0.1, 0.2); // let the game set up its permutation
            bool was = Enabled; Enabled = true;
            var rnd = new System.Random(7);
            long mism = 0; string first = "";
            for (int i = 0; i < n; i++)
            {
                double s = i % 4 == 0 ? 1e-3 : i % 4 == 1 ? 1 : i % 4 == 2 ? 1000 : 1e6;
                double x = (rnd.NextDouble() - 0.5) * s, y = (rnd.NextDouble() - 0.5) * s, z = (rnd.NextDouble() - 0.5) * s;
                if (i % 97 == 0) { x = System.Math.Round(x); z = System.Math.Round(z); } // whole numbers: fastfloor quirk
                double a2 = NoiseS3D.Noise(x, y), b2 = Noise2(x, y), a3 = NoiseS3D.Noise(x, y, z), b3 = Noise3(x, y, z);
                if (System.BitConverter.DoubleToInt64Bits(a2) != System.BitConverter.DoubleToInt64Bits(b2) ||
                    System.BitConverter.DoubleToInt64Bits(a3) != System.BitConverter.DoubleToInt64Bits(b3))
                { mism++; if (first.Length == 0) first = $"({x},{y},{z}) 2D {a2:R}/{b2:R} 3D {a3:R}/{b3:R}"; }
            }
            var sw = System.Diagnostics.Stopwatch.StartNew(); double sink = 0;
            for (int i = 0; i < 200000; i++) sink += NoiseS3D.Noise(i * 0.37, 12345.0, i * 0.11);
            double tOrig = sw.Elapsed.TotalMilliseconds; sw.Restart();
            for (int i = 0; i < 200000; i++) sink += Noise3(i * 0.37, 12345.0, i * 0.11);
            double tFast = sw.Elapsed.TotalMilliseconds;
            Enabled = was;
            return "{\"checked\":" + n + ",\"mismatches\":" + mism + ",\"first\":" + Json.Str(first) + ",\"orig3dNs\":" + (tOrig * 1e6 / 200000).ToString("F0") +
                   ",\"fast3dNs\":" + (tFast * 1e6 / 200000).ToString("F0") + ",\"sink\":" + (sink != 0 ? 1 : 0) + ",\"patched\":" + Patched + "}";
        }
    }
}
