using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Batch mode: Unity.exe -batchmode -quit -projectPath <this> -executeMethod BuildFsrBundle.Build
// Builds Out/fsr3upscaler.bundle with FSR3Unity's FSR 3.1 upscaler compute shaders (MIT) for 64-bit Windows,
// compiled for Direct3D 11 (what The Long Drive uses) and Direct3D 12. LZ4 chunks (tools/privacy_scan.py unpacks them
// to check the strings inside) and deterministic, so the same sources give the same file. Copy it to
// src/TLDRevamp/Assets/Fsr3Upscaler.bundle (embedded in TLDRevamp.dll). Needs the game's Unity: 2022.3.12f1.
public static class BuildFsrBundle
{
    public static void Build()
    {
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11, GraphicsDeviceType.Direct3D12 });
        var paths = AssetDatabase.FindAssets("t:ComputeShader")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => Path.GetFileName(p).StartsWith("ffx_fsr3upscaler_"))
            .OrderBy(p => p).ToArray();
        Directory.CreateDirectory("Out");
        var build = new AssetBundleBuild { assetBundleName = "fsr3upscaler.bundle", assetNames = paths };
        var manifest = BuildPipeline.BuildAssetBundles("Out", new[] { build },
            BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.DeterministicAssetBundle | BuildAssetBundleOptions.StrictMode, BuildTarget.StandaloneWindows64);
        Debug.Log("FSRBUNDLE " + (manifest != null ? "built" : "FAILED") + ", " + paths.Length + " shaders: " + string.Join(", ", paths.Select(Path.GetFileNameWithoutExtension)));
        if (manifest == null) EditorApplication.Exit(1);
    }
}
