using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityEngine.Rendering { public enum ShadowCastingMode { Off, On, TwoSided, ShadowsOnly } }
namespace UnityEngine
{
    public partial class Renderer { public Rendering.ShadowCastingMode shadowCastingMode = Rendering.ShadowCastingMode.On; }
    public enum LightShadows { None, Hard, Soft }
    public partial class Light : Component { public LightShadows shadows; }
    public class Animator : Component { }
    public enum Space { Self, World }
    public partial class Transform { public void Rotate(Vector3 axis, float angle, Space space) { } }
    public partial struct Quaternion { public static Quaternion FromToRotation(Vector3 a, Vector3 b) => identity; }
    public partial class WheelCollider
    {
        public float steerAngle;
        public Vector3 Pose;
        public void GetWorldPose(out Vector3 p, out Quaternion q) { p = Pose; q = Quaternion.identity; }
    }
    public static partial class Mathf
    {
        public static float Pow(float a, float b) => (float)Math.Pow(a, b);
        public static float Repeat(float t, float length) => t - (float)Math.Floor(t / length) * length;
        public static float LerpUnclamped(float a, float b, float t) => a + (b - a) * t;
    }
    public static class PresentationAudio
    {
        public static void Play(this AudioSource source) { source.isPlaying = true; }
        public static void Stop(this AudioSource source) { source.isPlaying = false; }
    }
}
public class wheelgraphicsscript : Component
{
    public WheelCollider W, W2; public Transform T, keepUprightParent;
    public Vector3 keepUprightPAxis, keepUprightTAxis; public bool keepUpright;
    public void Graphics() { }
}
public partial class enginescript : Component
{
    public float TempWeak, minOptimalTemp2, maxOptimalTemp2 = 100, engineStartTimeModifier = 1, SStartVolume, SStopVolume;
    public float SMaxAlapRpm = 1000, SMinAlapRpm, SMaxAlapSound = 1, SMinMotorRpm, SMaxMotorRpm = 6000, SMaxMotorSound = 1;
    public float minAlapPitch, maxAlapPitch = 1, minMotorPitch, maxMotorPitch = 1, maxRpm = 6000;
    public AudioSource SAlap, SMotor; public AudioClip SStart, SStop; public bool kickstart;
}
public partial class gearstickscript { public int Kicks; public void Kick() { Kicks++; } }
public partial class carscript { public void DamageStuff(float v) { } }
public partial class tosaveitemscript { public void DistFreezeRB() { } public void DistUnFreezeRB() { } }
public partial class attachablescript { public bool isPart; }
public class randomFlyer : Component { }
namespace TLDRevamp.Net
{
    public static partial class Entities
    {
        public sealed partial class Ent
        {
            public wheelgraphicsscript[] Wheels;
            public float[] WTravel, WSteer, WRpm, WSpin, WBrake, WMotor;
            public bool HasTorques, EngRunning, EngStart, EngHasTemp, EngWasRunning, ReceivedAtRest;
            public float EngRpm, EngTemp, EngStarterAt;
        }
        internal static readonly Dictionary<enginescript, Ent> ProxyEngineOwner = new Dictionary<enginescript, Ent>();
        internal static readonly Dictionary<wheelgraphicsscript, Ent> ProxyWheelOwner = new Dictionary<wheelgraphicsscript, Ent>();
        internal static readonly Dictionary<wheelgraphicsscript, int> ProxyWheelIndex = new Dictionary<wheelgraphicsscript, int>();
        private static bool NearOtherPlayersCar(Ent e) => ByNet.Values.Any(o => o.Proxy && o.Root != null && o.PartIndex < 0 && o.Root.car != null && (o.Root.transform.position - e.Root.transform.position).sqrMagnitude < TorqueRadiusM * TorqueRadiusM);
        private static IEnumerable<carscript> CopyCars()
        {
            if (CopyLodAllCars) { foreach (var car in UnityEngine.Object.FindObjectsOfType<carscript>()) yield return car; yield break; }
            foreach (var e in ByNet.Values) if (e.Proxy && e.Root != null && e.Root.car != null) yield return e.Root.car;
        }
    }
    internal static partial class PresentationFloorFixture { internal static bool Seen; }
}
