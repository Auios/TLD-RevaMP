using System;
using System.Collections.Generic;
using System.Reflection;

namespace TLDRevamp
{
    internal static class Plugin
    {
        internal static int PatchFailures;
        internal static readonly List<string> FailedPatches = new List<string>();
    }
    internal static class TelemetryPrivacy
    {
        internal static string LookupStr(string field, string text) => System.Text.Json.JsonSerializer.Serialize(text);
    }
}

// Only the registry's recording/reporting is exercised with these stand-ins. Offline resolution uses real metadata.
namespace HarmonyLib
{
    internal enum MethodType { Normal, Getter, Setter, Constructor, StaticConstructor }
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    internal sealed class HarmonyPatch : Attribute
    {
        internal HarmonyPatch(Type declaringType) { }
        internal HarmonyPatch(string methodName, Type[] argumentTypes) { }
        internal HarmonyPatch(string methodName, MethodType methodType) { }
    }
    public static class AccessTools
    {
        public delegate ref F FieldRef<T, F>(T instance);
        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        internal static FieldInfo Field(Type type, string name) => type?.GetField(name, All);
        internal static MethodInfo Method(Type type, string name, Type[] parameters = null) => parameters == null ? type?.GetMethod(name, All) : type?.GetMethod(name, All, null, parameters, null);
        internal static PropertyInfo Property(Type type, string name) => type?.GetProperty(name, All);
        internal static MethodInfo PropertyGetter(Type type, string name) => Property(type, name)?.GetGetMethod(true);
        internal static MethodInfo PropertySetter(Type type, string name) => Property(type, name)?.GetSetMethod(true);
        internal static ConstructorInfo Constructor(Type type, Type[] parameters = null) => type?.GetConstructor(All, null, parameters ?? Type.EmptyTypes, null);
        internal static Type Inner(Type type, string name) => type?.GetNestedType(name, All);
        internal static Type TypeByName(string name) => Type.GetType(name);
        internal static FieldRef<T, F> FieldRefAccess<T, F>(string name) => throw new MissingFieldException(name);
    }
}

