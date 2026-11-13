using System.Linq;
using System;
using System.Collections.Generic;
using static TLDRevamp.Fixes.FastRandom;

namespace TLDRevamp.Fixes
{
    internal static class FastRandomTools
    {

        /// Compare with the real System.Random for n seeds spread over the whole int range (incl. edge cases).
        public static string Check(int n)
        {
            long mismatches = 0;
            string first = "";
            var rnd = new System.Random(12345);
            int[] edge = { 0, 1, -1, int.MaxValue, int.MinValue, int.MinValue + 1, MSEED, -MSEED };
            for (int i = 0; i < n + edge.Length; i++)
            {
                int seed = i < edge.Length ? edge[i] : rnd.Next(int.MinValue, int.MaxValue);
                double real = new System.Random(seed).NextDouble();
                double fast = FirstDouble(seed);
                if (real != fast)
                {
                    mismatches++;
                    if (first.Length == 0) first = seed + ": " + real.ToString("R") + " vs " + fast.ToString("R");
                }
            }
            var sw = System.Diagnostics.Stopwatch.StartNew();
            double sink = 0;
            for (int i = 0; i < 20000; i++) sink += FirstDouble(i * 7919);
            double us = sw.Elapsed.TotalMilliseconds * 1000.0 / 20000;
            sw.Restart();
            for (int i = 0; i < 20000; i++) sink += new System.Random(i * 7919).NextDouble();
            double realUs = sw.Elapsed.TotalMilliseconds * 1000.0 / 20000;
            return "{\"checked\":" + (n + edge.Length) + ",\"mismatches\":" + mismatches + ",\"first\":" + Json.Str(first) +
                   ",\"fastUs\":" + us.ToString("F3") + ",\"realUs\":" + realUs.ToString("F3") + ",\"sink\":" + (sink > 0 ? 1 : 0) + "}";
        }
    }
}
