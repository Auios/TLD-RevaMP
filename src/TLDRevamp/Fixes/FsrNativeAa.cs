using System;
using System.IO;
using System.Linq;
using FidelityFX;
using FidelityFX.FSR3;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace TLDRevamp.Fixes
{
    /// AMD FidelityFX FSR 3.1 as anti-aliasing at full resolution ("native AA", upscaling ratio 1.0), one of the filters in
    /// Settings > Revamp > Anti-aliasing (AntiAliasingChoice). FSR blends slightly shifted frames like TAA, but follows
    /// every pixel back with depth and motion vectors and rejects what it can't follow, so it smooths edges and thin lines
    /// without TAA's shimmer on speckled glass. Measured 2026-11-18 on a player's save (1080p, RTX 4070, a car carrying
    /// ~960 mounted items and a pile of glass bottles in view): +0.3 ms a frame (8x MSAA: +8.6 ms), 0.02% of the bottles'
    /// pixels changing visibly from frame to frame (8x MSAA 0.04%, tuned TAA 4%), edges smoother than 8x MSAA, no trails
    /// while turning at 30 degrees a second.
    ///
    /// The upscaler is FSR3Unity (Fsr3Unity/, MIT, Nico de Poel), AMD's FSR 3.1 backend rewritten in C# on
    /// Unity's compute shaders; the shaders are AMD's (MIT) compiled for this game's Unity (2022.3.12f1) into
    /// Assets/Fsr3Upscaler.bundle, embedded in this DLL (tools/fsr3bundle rebuilds it). Inputs as FSR3Unity's own
    /// built-in-pipeline effect feeds them: the camera jittered by FSR's sequence, the camera target, the depth buffer
    /// and Unity's motion vectors; it runs before the post-processing stack, on the camera that has it (the near camera).
    public static class FsrNativeAa
    {
        private const string Resource = "TLDRevamp.Fsr3Upscaler.bundle";
        private static Fsr3UpscalerShaders _shaders;
        private static bool _tried;
        internal static string Status = "not loaded";

        /// FSR needs compute shaders with typed UAV loads; every GPU this game runs on has them, but say so if not.
        internal static bool Supported => SystemInfo.supportsComputeShaders;

        internal static bool Load()
        {
            if (_tried) return _shaders != null;
            _tried = true;
            try
            {
                if (!Supported) { Status = "no compute shader support"; Plugin.Log.LogWarning("FSR: " + Status); return false; }
                byte[] data;
                using (var s = typeof(FsrNativeAa).Assembly.GetManifestResourceStream(Resource))
                {
                    if (s == null) { Status = "shader bundle missing from the DLL"; Plugin.Log.LogWarning("FSR: " + Status); return false; }
                    using (var m = new MemoryStream()) { s.CopyTo(m); data = m.ToArray(); }
                }
                var bundle = AssetBundle.LoadFromMemory(data);
                if (!bundle) { Status = "shader bundle did not load"; Plugin.Log.LogWarning("FSR: " + Status); return false; }
                var all = bundle.LoadAllAssets<ComputeShader>();
                ComputeShader Get(string n) => all.FirstOrDefault(c => c.name == "ffx_fsr3upscaler_" + n);
                var shaders = new Fsr3UpscalerShaders
                {
                    prepareInputsPass = Get("prepare_inputs_pass"), lumaPyramidPass = Get("luma_pyramid_pass"),
                    shadingChangePyramidPass = Get("shading_change_pyramid_pass"), shadingChangePass = Get("shading_change_pass"),
                    prepareReactivityPass = Get("prepare_reactivity_pass"), lumaInstabilityPass = Get("luma_instability_pass"),
                    accumulatePass = Get("accumulate_pass"), sharpenPass = Get("rcas_pass"), autoGenReactivePass = Get("autogen_reactive_pass"),
                    tcrAutoGenPass = Get("tcr_autogen_pass"), debugViewPass = Get("debug_view_pass"),
                };
                int missing = typeof(Fsr3UpscalerShaders).GetFields().Count(f => f.GetValue(shaders) == null);
                Status = all.Length + " FSR shaders" + (missing > 0 ? ", " + missing + " missing" : "");
                Plugin.Log.LogInfo("FSR 3.1: " + Status);
                if (missing == 0) _shaders = shaders;
                return _shaders != null;
            }
            catch (Exception e) { Status = "failed: " + e.Message; Plugin.Log.LogWarning("FSR: " + Status); return false; }
        }

        internal static Fsr3UpscalerShaders Shaders => _shaders;

        internal static void Attach(Camera cam)
        {
            if (!cam || !Load()) return;
            if (!cam.GetComponent<FsrNativeAaCamera>()) cam.gameObject.AddComponent<FsrNativeAaCamera>();
        }

        internal static void Detach(Camera cam)
        {
            var c = cam ? cam.GetComponent<FsrNativeAaCamera>() : null;
            if (c) UnityEngine.Object.Destroy(c);
        }

        internal static void DetachAll()
        {
            if (FsrNativeAaCamera.Live == 0) return;
            foreach (var c in UnityEngine.Object.FindObjectsOfType<FsrNativeAaCamera>()) UnityEngine.Object.Destroy(c);
        }
    }

    /// FSR on one camera: jitter before culling, FSR in a command buffer before the post-processing stack's own.
    [DefaultExecutionOrder(1000)]
    internal sealed class FsrNativeAaCamera : MonoBehaviour
    {
        private static readonly int Output = Shader.PropertyToID("TLDRevampFsrOutput");
        private Camera _cam;
        private CommandBuffer _cb;
        private Fsr3UpscalerContext _ctx;
        private readonly Fsr3Upscaler.DispatchDescription _d = new Fsr3Upscaler.DispatchDescription();
        private Vector2Int _size;
        private bool _reset = true;
        internal static long Frames;
        internal static int Live;

        private void OnEnable()
        {
            Live++;
            _cam = GetComponent<Camera>();
            _cam.depthTextureMode |= DepthTextureMode.Depth | DepthTextureMode.MotionVectors;
            _cb = new CommandBuffer { name = "TLD Revamp FSR 3.1" };
            // before the post-processing stack's buffer on the same event: re-add its buffers after ours
            var others = _cam.GetCommandBuffers(CameraEvent.BeforeImageEffects);
            foreach (var b in others) _cam.RemoveCommandBuffer(CameraEvent.BeforeImageEffects, b);
            _cam.AddCommandBuffer(CameraEvent.BeforeImageEffects, _cb);
            foreach (var b in others) _cam.AddCommandBuffer(CameraEvent.BeforeImageEffects, b);
            _reset = true;
        }

        private void OnDisable()
        {
            Live--;
            if (_cam) { _cam.RemoveCommandBuffer(CameraEvent.BeforeImageEffects, _cb); _cam.ResetProjectionMatrix(); }
            _cb?.Release(); _cb = null;
            _ctx?.Destroy(); _ctx = null;
        }

        private void Ensure(Vector2Int size)
        {
            if (_ctx != null && size == _size) return;
            _ctx?.Destroy();
            _size = size;
            var flags = Fsr3Upscaler.InitializationFlags.EnableAutoExposure;
            if (_cam.allowHDR) flags |= Fsr3Upscaler.InitializationFlags.EnableHighDynamicRange;
            if (SystemInfo.usesReversedZBuffer) flags |= Fsr3Upscaler.InitializationFlags.EnableDepthInverted;
            _ctx = Fsr3Upscaler.CreateContext(size, size, FsrNativeAa.Shaders, flags);
            _reset = true;
        }

        private void OnPreCull()
        {
            if (!_cam || FsrNativeAa.Shaders == null) return;
            var size = new Vector2Int(_cam.pixelWidth, _cam.pixelHeight);
            if (size.x <= 0 || size.y <= 0) return;
            Ensure(size);
            _cam.ResetProjectionMatrix();
            int phases = Fsr3Upscaler.GetJitterPhaseCount(size.x, size.x);
            Fsr3Upscaler.GetJitterOffset(out float jx, out float jy, Time.frameCount, phases);
            _d.JitterOffset = new Vector2(jx, jy);
            var shift = Matrix4x4.Translate(new Vector3(2f * jx / size.x, 2f * jy / size.y, 0));
            _cam.nonJitteredProjectionMatrix = _cam.projectionMatrix;
            _cam.projectionMatrix = shift * _cam.nonJitteredProjectionMatrix;
            _cam.useJitteredProjectionMatrixForTransparentRendering = true;
        }

        private void OnPreRender()
        {
            if (_ctx == null || _cb == null) return;
            _d.Color = new ResourceView(BuiltinRenderTextureType.CameraTarget, RenderTextureSubElement.Color);
            _d.Depth = new ResourceView(BuiltinRenderTextureType.Depth, RenderTextureSubElement.Depth);
            _d.MotionVectors = new ResourceView(BuiltinRenderTextureType.MotionVectors);
            _d.Exposure = ResourceView.Unassigned; _d.Reactive = ResourceView.Unassigned; _d.TransparencyAndComposition = ResourceView.Unassigned;
            _d.Output = new ResourceView(Output);
            _d.PreExposure = 1f; _d.EnableSharpening = false; _d.Sharpness = 0f;
            _d.MotionVectorScale = new Vector2(-_size.x, -_size.y);   // Unity's motion vectors are in UV units, current minus previous
            _d.RenderSize = _size; _d.UpscaleSize = _size;
            _d.FrameTimeDelta = Time.unscaledDeltaTime;
            _d.CameraNear = _cam.nearClipPlane; _d.CameraFar = _cam.farClipPlane;
            if (SystemInfo.usesReversedZBuffer) (_d.CameraNear, _d.CameraFar) = (_d.CameraFar, _d.CameraNear);   // FSR wants them swapped then
            _d.CameraFovAngleVertical = _cam.fieldOfView * Mathf.Deg2Rad;
            _d.ViewSpaceToMetersFactor = 1f;
            _d.Reset = _reset; _reset = false;
            _cb.Clear();
            _cb.GetTemporaryRT(Output, _size.x, _size.y, 0, default, _cam.allowHDR ? GraphicsFormat.R16G16B16A16_SFloat : GraphicsFormat.R8G8B8A8_UNorm, 1, true);
            _ctx.Dispatch(_d, _cb);
            _cb.Blit(Output, BuiltinRenderTextureType.CameraTarget);
            _cb.ReleaseTemporaryRT(Output);
            Frames++;
        }

        private void OnPostRender() { if (_cam) _cam.ResetProjectionMatrix(); }
    }
}
