using System;
using System.Reflection;

namespace TLDRevamp
{
    // Loader tests exercise retargeting with their own fixture types, without the game registry's startup state.
    internal static class GameLookups
    {
        internal static FieldInfo RuntimeField(Type type, string name, BindingFlags flags) => type.GetField(name, flags);
        internal static Type RuntimeType(Assembly assembly, string name, bool throwOnError) => assembly.GetType(name, throwOnError);
    }
}
