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
    internal static class GameLookups
    {
        internal static System.Type RuntimeFoundType(string name, System.Type found) => found;
        internal static System.Collections.Generic.IEnumerable<System.Reflection.MethodInfo> RuntimeMethods(System.Type type, string name, System.Reflection.BindingFlags flags) => System.Linq.Enumerable.Where(type.GetMethods(flags), m => m.Name == name);
        internal static System.Reflection.FieldInfo RuntimeField(System.Type type, string name, System.Reflection.BindingFlags flags) => type.GetField(name, flags);
        internal static System.Reflection.PropertyInfo RuntimeProperty(System.Type type, string name, System.Reflection.BindingFlags flags) => type.GetProperty(name, flags);
    }
    internal static class Json
    {
        internal static string Str(string value) => System.Text.Json.JsonSerializer.Serialize(value);
    }
}
