using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace TLDRevamp.Fixes
{
    /// NoiseS3D (the game's simplex noise) is the hottest function of world generation: every terrain height is a sum of
    /// fractal layers of 3D simplex noise (x, seed, z), road shapes use 2D noise, biomes more 3D noise. The game's version
    /// is the classic reference written slowly: `perm` is a property with a null check read 12 times per 3D call,
    /// 4 `% 12` divisions, gradients in a jagged int[][] passed to separate dot() calls, fastfloor() calls.
    ///
    /// This is simplex noise as Stefan Gustavson's public-domain reference gives it ("Simplex noise demystified", 2005,
    /// and its SimplexNoise.java), written for speed without changing a single floating-point operation or its order
    /// (bit-identical results, checked by `noisecheck`): the permutation table is the game's own (read once per call),
    /// `perm[i] % 12` comes from a table built from it, the gradients from a flat array, the helpers inlined —
    /// including the reference's fastfloor, which floors whole negative numbers one lower.
    /// The game's callers (terrain noise layers, road shapes) call this directly (GamePatch swaps the call), so there
    /// is no wrapper cost.
    ///
    /// Runtime switch (bridge `set`): TLDRevamp.Fixes.FastNoise.Enabled
    [HarmonyPatch]
    public static class FastNoise
    {
        public static bool Enabled = false;
        public static int Patched;

        private static readonly AccessTools.FieldRef<int[]> PermRef = AccessTools.StaticFieldRefAccess<int[]>(GameLookups.Field(typeof(NoiseS3D), "perm_"));
        private static readonly MethodInfo Game2 = GameLookups.Method(typeof(NoiseS3D), nameof(NoiseS3D.Noise), new[] { typeof(double), typeof(double) });
        private static readonly MethodInfo Game3 = GameLookups.Method(typeof(NoiseS3D), nameof(NoiseS3D.Noise), new[] { typeof(double), typeof(double), typeof(double) });

        // The reference's 12 gradients (the edge midpoints of a cube), flattened, as doubles
        private static readonly double[] Grad3 = { 1, 1, 0, -1, 1, 0, 1, -1, 0, -1, -1, 0, 1, 0, 1, -1, 0, 1, 1, 0, -1, -1, 0, -1, 0, 1, 1, 0, -1, 1, 0, 1, -1, 0, -1, -1 };

        internal sealed class Tables { public int[] Perm; public int[] PermMod12; }
        private static Tables _t;

        internal static Tables Get()
        {
            var perm = PermRef();
            var t = _t;
            if (t != null && ReferenceEquals(t.Perm, perm)) return t;
            if (perm == null) return null; // not set up yet: the game sets it up
            var mod = new int[perm.Length];
            for (int i = 0; i < perm.Length; i++) mod[i] = perm[i] % 12;
            t = new Tables { Perm = perm, PermMod12 = mod };
            _t = t; // reference assignment is atomic; workers may build their own copy once, results are identical
            return t;
        }

        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return GameLookups.Method(typeof(terrainGenSettings.noiseSetting), nameof(terrainGenSettings.noiseSetting.NoOctaveNoise));
            yield return GameLookups.Method(typeof(roadGenScript), nameof(roadGenScript.PointOnRoad));
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            var p = new GamePatch(nameof(FastNoise) + "." + original.Name, instructions);
            if (original.Name == nameof(roadGenScript.PointOnRoad)) p.Calls(Game2, AccessTools.Method(typeof(FastNoise), nameof(Noise2)), 1);
            else p.Calls(Game3, AccessTools.Method(typeof(FastNoise), nameof(Noise3)), 1);
            int ok = 0;
            var code = p.Result(ref ok);
            Patched += ok;
            return code;
        }

        // The reference's skewing factors: F2/G2 as it computes them, F3 = 1/3, G3 = 1/6
        private static readonly double F2 = 0.5 * (System.Math.Sqrt(3.0) - 1.0);
        private static readonly double G2 = (3.0 - System.Math.Sqrt(3.0)) / 6.0;
        private const double F3 = 1.0 / 3.0, G3 = 1.0 / 6.0;

        /// The reference's fastfloor: x > 0 ? (int)x : (int)x - 1.
        private static int FastFloor(double x) => x > 0.0 ? (int)x : (int)x - 1;

        /// 2D simplex noise (the reference's noise(xin, yin)).
        public static double Noise2(double xin, double yin)
        {
            var tb = Enabled ? Get() : null;
            if (tb == null) return NoiseS3D.Noise(xin, yin);
            var perm = tb.Perm; var permMod12 = tb.PermMod12; var g = Grad3;
            // skew the input space to find the simplex cell
            double s = (xin + yin) * F2;
            int i = FastFloor(xin + s);
            int j = FastFloor(yin + s);
            double t = (double)(i + j) * G2;
            double x0 = xin - ((double)i - t);   // distances from the cell origin
            double y0 = yin - ((double)j - t);
            // the middle corner: lower or upper triangle
            int i1, j1;
            if (x0 > y0) { i1 = 1; j1 = 0; } else { i1 = 0; j1 = 1; }
            double x1 = x0 - (double)i1 + G2;
            double y1 = y0 - (double)j1 + G2;
            double x2 = x0 - 1.0 + 2.0 * G2;
            double y2 = y0 - 1.0 + 2.0 * G2;
            int ii = i & 0xFF, jj = j & 0xFF;
            int gi0 = permMod12[ii + perm[jj]] * 3;
            int gi1 = permMod12[ii + i1 + perm[jj + j1]] * 3;
            int gi2 = permMod12[ii + 1 + perm[jj + 1]] * 3;
            // each corner's contribution
            double n0, n1, n2;
            double t0 = 0.5 - x0 * x0 - y0 * y0;
            if (t0 < 0.0) n0 = 0.0;
            else { t0 *= t0; n0 = t0 * t0 * (g[gi0] * x0 + g[gi0 + 1] * y0); }
            double t1 = 0.5 - x1 * x1 - y1 * y1;
            if (t1 < 0.0) n1 = 0.0;
            else { t1 *= t1; n1 = t1 * t1 * (g[gi1] * x1 + g[gi1 + 1] * y1); }
            double t2 = 0.5 - x2 * x2 - y2 * y2;
            if (t2 < 0.0) n2 = 0.0;
            else { t2 *= t2; n2 = t2 * t2 * (g[gi2] * x2 + g[gi2 + 1] * y2); }
            return 70.0 * (n0 + n1 + n2);
        }

        /// 3D simplex noise (the reference's noise(xin, yin, zin)).
        public static double Noise3(double xin, double yin, double zin)
        {
            var tb = Enabled ? Get() : null;
            if (tb == null) return NoiseS3D.Noise(xin, yin, zin);
            var perm = tb.Perm; var permMod12 = tb.PermMod12; var g = Grad3;
            double s = (xin + yin + zin) * F3;
            int i = FastFloor(xin + s);
            int j = FastFloor(yin + s);
            int k = FastFloor(zin + s);
            double t = (double)(i + j + k) * G3;
            double x0 = xin - ((double)i - t);
            double y0 = yin - ((double)j - t);
            double z0 = zin - ((double)k - t);
            // which of the six tetrahedra: the second and third corners' offsets
            int i1, j1, k1, i2, j2, k2;
            if (x0 >= y0)
            {
                if (y0 >= z0) { i1 = 1; j1 = 0; k1 = 0; i2 = 1; j2 = 1; k2 = 0; }
                else if (x0 >= z0) { i1 = 1; j1 = 0; k1 = 0; i2 = 1; j2 = 0; k2 = 1; }
                else { i1 = 0; j1 = 0; k1 = 1; i2 = 1; j2 = 0; k2 = 1; }
            }
            else if (y0 < z0) { i1 = 0; j1 = 0; k1 = 1; i2 = 0; j2 = 1; k2 = 1; }
            else if (x0 < z0) { i1 = 0; j1 = 1; k1 = 0; i2 = 0; j2 = 1; k2 = 1; }
            else { i1 = 0; j1 = 1; k1 = 0; i2 = 1; j2 = 1; k2 = 0; }
            double x1 = x0 - (double)i1 + G3;
            double y1 = y0 - (double)j1 + G3;
            double z1 = z0 - (double)k1 + G3;
            double x2 = x0 - (double)i2 + 2.0 * G3;
            double y2 = y0 - (double)j2 + 2.0 * G3;
            double z2 = z0 - (double)k2 + 2.0 * G3;
            double x3 = x0 - 1.0 + 3.0 * G3;
            double y3 = y0 - 1.0 + 3.0 * G3;
            double z3 = z0 - 1.0 + 3.0 * G3;
            int ii = i & 0xFF, jj = j & 0xFF, kk = k & 0xFF;
            int gi0 = permMod12[ii + perm[jj + perm[kk]]] * 3;
            int gi1 = permMod12[ii + i1 + perm[jj + j1 + perm[kk + k1]]] * 3;
            int gi2 = permMod12[ii + i2 + perm[jj + j2 + perm[kk + k2]]] * 3;
            int gi3 = permMod12[ii + 1 + perm[jj + 1 + perm[kk + 1]]] * 3;
            double n0, n1, n2, n3;
            double t0 = 0.6 - x0 * x0 - y0 * y0 - z0 * z0;
            if (t0 < 0.0) n0 = 0.0;
            else { t0 *= t0; n0 = t0 * t0 * (g[gi0] * x0 + g[gi0 + 1] * y0 + g[gi0 + 2] * z0); }
            double t1 = 0.6 - x1 * x1 - y1 * y1 - z1 * z1;
            if (t1 < 0.0) n1 = 0.0;
            else { t1 *= t1; n1 = t1 * t1 * (g[gi1] * x1 + g[gi1 + 1] * y1 + g[gi1 + 2] * z1); }
            double t2 = 0.6 - x2 * x2 - y2 * y2 - z2 * z2;
            if (t2 < 0.0) n2 = 0.0;
            else { t2 *= t2; n2 = t2 * t2 * (g[gi2] * x2 + g[gi2 + 1] * y2 + g[gi2 + 2] * z2); }
            double t3 = 0.6 - x3 * x3 - y3 * y3 - z3 * z3;
            if (t3 < 0.0) n3 = 0.0;
            else { t3 *= t3; n3 = t3 * t3 * (g[gi3] * x3 + g[gi3 + 1] * y3 + g[gi3 + 2] * z3); }
            return 32.0 * (n0 + n1 + n2 + n3);
        }
    }
}
