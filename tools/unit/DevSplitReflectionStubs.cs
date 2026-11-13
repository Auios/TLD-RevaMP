namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    }
}

namespace TLDRevamp
{
    internal static class Json
    {
        internal static string Str(string value) => System.Text.Json.JsonSerializer.Serialize(value);
    }
}
