using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;

namespace TLDRevamp
{
    /// Game member names and their results, kept once per source site and signature.
    public static class GameLookups
    {
        private sealed class Entry
        {
            internal string Kind, Type, Member, Parameters, File;
            internal int Line;
            internal bool Found;
            internal string Description => Type + "." + Member + " (" + Kind + ") at " + File + ":" + Line;
        }

        private readonly struct Key : IEquatable<Key>
        {
            private readonly string _kind, _member, _file;
            private readonly Type _type;
            private readonly Type[] _parameters;
            private readonly int _line;
            internal Key(string kind, Type type, string member, Type[] parameters, string file, int line)
            { _kind = kind; _type = type; _member = member; _parameters = parameters; _file = file; _line = line; }
            internal Key Copy() => new Key(_kind, _type, _member, _parameters == null ? null : (Type[])_parameters.Clone(), _file, _line);
            public bool Equals(Key other)
            {
                if (_kind != other._kind || _type != other._type || _member != other._member || _file != other._file || _line != other._line) return false;
                if (_parameters == null || other._parameters == null) return _parameters == other._parameters;
                if (_parameters.Length != other._parameters.Length) return false;
                for (int i = 0; i < _parameters.Length; i++) if (_parameters[i] != other._parameters[i]) return false;
                return true;
            }
            public override bool Equals(object obj) => obj is Key key && Equals(key);
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = _kind.GetHashCode();
                    hash = hash * 31 + (_type?.GetHashCode() ?? 0); hash = hash * 31 + (_member?.GetHashCode() ?? 0);
                    hash = hash * 31 + _file.GetHashCode(); hash = hash * 31 + _line;
                    if (_parameters != null) foreach (var parameter in _parameters) hash = hash * 31 + (parameter?.GetHashCode() ?? 0);
                    return hash;
                }
            }
        }

        private static readonly object Gate = new object();
        private static readonly Dictionary<Key, Entry> Entries = new Dictionary<Key, Entry>();
        private static Action<string> _late;
        private static int _applied;
        private static readonly Dictionary<(Type, string, BindingFlags, bool), FieldInfo> Fields = new Dictionary<(Type, string, BindingFlags, bool), FieldInfo>();
        private static readonly Dictionary<(Type, string, BindingFlags, string, int, bool), FieldInfo> FieldSites = new Dictionary<(Type, string, BindingFlags, string, int, bool), FieldInfo>();
        private static readonly Dictionary<string, string> MemberNames = new Dictionary<string, string>();
        private static readonly Dictionary<Assembly, bool> GameAssemblies = new Dictionary<Assembly, bool>();
        private static readonly Dictionary<MethodInfo, Type[]> MethodParameters = new Dictionary<MethodInfo, Type[]>();

        private static Type[] Parameters(MethodInfo method)
        {
            lock (Gate)
            {
                if (!MethodParameters.TryGetValue(method, out var parameters))
                { parameters = Array.ConvertAll(method.GetParameters(), p => p.ParameterType); MethodParameters.Add(method, parameters); }
                return parameters;
            }
        }

        private static T Record<T>(T result, string kind, Type type, string member, Type[] parameters, string file, int line) where T : class
        {
            var key = new Key(kind, type, member, parameters, file, line);
            Action<string> log = null;
            Entry entry;
            lock (Gate)
            {
                if (Entries.TryGetValue(key, out entry)) { if (result != null) entry.Found = true; return result; }
                // Only a code path leaves the registry: never the build machine's parent directories.
                file = file.Replace('\\', '/');
                int start = file.LastIndexOf("/src/", StringComparison.Ordinal);
                file = start >= 0 ? file.Substring(start + 1) : Path.GetFileName(file);
                string args = parameters == null ? null : string.Join(",", Array.ConvertAll(parameters, t => t?.FullName ?? "null"));
                entry = new Entry { Kind = kind, Type = type?.FullName ?? "<null>", Member = member, Parameters = args, Found = result != null, File = file, Line = line };
                Entries.Add(key.Copy(), entry);
                if (!entry.Found) log = _late;
            }
            log?.Invoke("Game lookup missing: " + entry.Description);
            return result;
        }

        public static FieldInfo Field(Type type, string name, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
            type == null ? Record(AccessTools.Field(type, name), "field", type, name, null, file, line) : CachedField(type, name, 0, file, line, true);
        public static FieldInfo OptionalField(Type type, string name, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
            CachedField(type, name, 0, file, line, true);
        public static MethodInfo Method(Type type, string name, Type[] parameters = null, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
            Record(AccessTools.Method(type, name, parameters), "method", type, name, parameters, file, line);
        public static IEnumerable<MethodInfo> Methods(Type type, string name, BindingFlags flags,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            bool found = false;
            foreach (var method in type.GetMethods(flags))
                if (method.Name == name)
                {
                    found = true;
                    yield return Record(method, "method", type, name, Parameters(method), file, line);
                }
            if (!found) Record<MethodInfo>(null, "method", type, name, null, file, line);
        }
        public static MethodInfo GenericMethod(Type type, string name, int parameterCount, Type lastParameter,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            var method = type.GetMethods().FirstOrDefault(m => m.Name == name && m.IsGenericMethodDefinition &&
                m.GetParameters().Length == parameterCount && m.GetParameters()[parameterCount - 1].ParameterType == lastParameter);
            Record(method, "generic method", type, name, method == null ? null : Parameters(method), file, line);
            return method ?? throw new InvalidOperationException("Sequence contains no matching element");
        }
        public static PropertyInfo Property(Type type, string name, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
            Record(AccessTools.Property(type, name), "property", type, name, null, file, line);
        public static MethodInfo PropertyGetter(Type type, string name, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
            Record(AccessTools.PropertyGetter(type, name), "getter", type, name, null, file, line);
        public static MethodInfo PropertySetter(Type type, string name, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
            Record(AccessTools.PropertySetter(type, name), "setter", type, name, null, file, line);
        public static ConstructorInfo Constructor(Type type, Type[] parameters = null, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
            Record(AccessTools.Constructor(type, parameters), "constructor", type, ".ctor", parameters, file, line);
        public static Type Inner(Type type, string name, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
            Record(AccessTools.Inner(type, name), "inner", type, name, null, file, line);
        public static Type TypeByName(Type assemblyOwner, string name, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
            Record(AccessTools.TypeByName(name), "type", assemblyOwner, name, null, file, line);
        public static Type AssemblyType(Type assemblyOwner, string name, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
            Record(assemblyOwner.Assembly.GetType(name), "type", assemblyOwner, name, null, file, line);
        public static MethodInfo NamedMethod(Type assemblyOwner, string member, Type runtimeType, BindingFlags flags,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
            ReflectionMethod(runtimeType, member.Substring(member.LastIndexOf(':') + 1), flags, null, file, line);

        public static AccessTools.FieldRef<T, F> FieldRefAccess<T, F>(Type type, string name, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            // Record a missing field even when Harmony throws while constructing the reference.
            Record(AccessTools.Field(type, name), "fieldref", type, name, null, file, line);
            return AccessTools.FieldRefAccess<T, F>(name);
        }

        // Type.GetField/GetProperty/GetMethod have different binding rules from AccessTools.
        public static FieldInfo ReflectionField(Type type, string name, BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
            Record(type.GetField(name, flags), "reflection field", type, name, null, file, line);
        public static PropertyInfo ReflectionProperty(Type type, string name, BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static,
            Type runtimeType = null,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
            Record((runtimeType ?? type).GetProperty(name, flags), "reflection property", runtimeType ?? type, name, null, file, line);
        public static MethodInfo ReflectionMethod(Type type, string name, BindingFlags flags, Type[] parameters = null,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
            Record(parameters == null ? type.GetMethod(name, flags) : type.GetMethod(name, flags, null, parameters, null), "reflection method", type, name, parameters, file, line);

        // These names are supplied by a bridge user or a mod binary, or discovered from generated classes.
        // There is no fixed game dependency to check offline. Keep the exact sites in the guard's allowlist.
        public static FieldInfo RuntimeField(Type type, string name, BindingFlags flags,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
            IsGame(type) ? CachedField(type, name, flags, file, line) : type.GetField(name, flags);
        public static PropertyInfo RuntimeProperty(Type type, string name, BindingFlags flags,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            var result = type.GetProperty(name, flags);
            return IsGame(type) ? Record(result, "reflection property", type, name, null, file, line) : result;
        }
        public static MethodInfo RuntimeMethod(Type type, string name, BindingFlags flags, Type[] parameters = null,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            var result = parameters == null ? type.GetMethod(name, flags) : type.GetMethod(name, flags, null, parameters, null);
            return IsGame(type) ? Record(result, "reflection method", type, name, parameters, file, line) : result;
        }
        public static IEnumerable<MethodInfo> RuntimeMethods(Type type, string name, BindingFlags flags,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            bool found = false;
            foreach (var method in type.GetMethods(flags))
                if (method.Name == name)
                {
                    found = true;
                    if (IsGame(type)) Record(method, "method", type, name, Parameters(method), file, line);
                    yield return method;
                }
            if (!found && IsGame(type)) Record<MethodInfo>(null, "method", type, name, null, file, line);
        }
        public static Type RuntimeFoundType(string name, Type result,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
            IsGame(result) ? Record(result, "type", result, name, null, file, line) : result;
        public static Type RuntimeType(Assembly assembly, string name, bool throwOnError = false,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            var result = assembly.GetType(name, false);
            if (IsGame(assembly)) Record(result, "type", null, name, null, file, line);
            return result ?? (throwOnError ? assembly.GetType(name, true) : null);
        }
        public static MethodInfo RuntimeHarmonyMethod(Type type, string name, Type[] parameters = null,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            var result = AccessTools.Method(type, name, parameters);
            return IsGame(type) ? Record(result, "method", type, name, parameters, file, line) : result;
        }
        public static MethodInfo RuntimeHarmonyGetter(Type type, string name,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            var result = AccessTools.PropertyGetter(type, name);
            return IsGame(type) ? Record(result, "getter", type, name, null, file, line) : result;
        }
        public static MethodInfo RuntimeHarmonySetter(Type type, string name,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            var result = AccessTools.PropertySetter(type, name);
            return IsGame(type) ? Record(result, "setter", type, name, null, file, line) : result;
        }
        public static ConstructorInfo RuntimeHarmonyConstructor(Type type, Type[] parameters,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            var result = AccessTools.Constructor(type, parameters);
            return IsGame(type) ? Record(result, "constructor", type, ".ctor", parameters, file, line) : result;
        }
        private static bool IsGame(Type type) => type != null && IsGame(type.Assembly);
        private static bool IsGame(Assembly assembly)
        {
            lock (Gate)
            {
                if (GameAssemblies.TryGetValue(assembly, out bool game)) return game;
                string name = assembly.GetName().Name;
                game = name.StartsWith("Assembly-CSharp", StringComparison.Ordinal) || name.StartsWith("UnityEngine", StringComparison.Ordinal)
                    || name.StartsWith("Unity.", StringComparison.Ordinal) || name.IndexOf("steamworks", StringComparison.OrdinalIgnoreCase) >= 0;
                GameAssemblies.Add(assembly, game);
                return game;
            }
        }

        /// An optional assembly's member, identified in source even when its type is only available at runtime.
        public static FieldInfo NamedField(Type assemblyOwner, string member, Type runtimeType,
            BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            string name;
            lock (Gate)
                if (!MemberNames.TryGetValue(member, out name)) { name = member.Substring(member.LastIndexOf(':') + 1); MemberNames.Add(member, name); }
            return CachedField(runtimeType, name, flags, file, line);
        }

        /// Save row types come from itemDataClass's lists; all of them use the same id member.
        public static FieldInfo SaveIdField(Type saveType, string name, Type rowType,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
            CachedField(rowType, name, BindingFlags.Public | BindingFlags.Instance, file, line);

        private static FieldInfo CachedField(Type type, string name, BindingFlags flags, string file, int line, bool harmony = false)
        {
            if (type == null) return null;
            FieldInfo field;
            lock (Gate)
            {
                var site = (type, name, flags, file, line, harmony);
                if (FieldSites.TryGetValue(site, out field)) return field;
                var key = (type, name, flags, harmony);
                if (!Fields.TryGetValue(key, out field)) { field = harmony ? AccessTools.Field(type, name) : type.GetField(name, flags); Fields[key] = field; }
                FieldSites[site] = field;
            }
            Record(field, harmony ? "field" : "reflection field", type, name, null, file, line);
            return field;
        }

        /// Keep Traverse's default value when a diagnostic field is absent.
        public static T FieldValue<T>(Type type, string name, object instance,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            if (instance == null) return default;
            var field = Field(instance?.GetType() ?? type, name, file, line);
            return field == null ? default : (T)field.GetValue(instance);
        }

        public static void PatchApplied() { lock (Gate) _applied++; }

        public static void Report(Action<string> log)
        {
            var b = new StringBuilder();
            lock (Gate)
            {
                int resolved = 0, missing = 0;
                foreach (var e in Entries.Values) { if (e.Found) resolved++; else missing++; }
                b.Append("Game lookups: ").Append(resolved).Append(" resolved, ").Append(missing).Append(" missing; patches: ").Append(_applied).Append(" applied, ").Append(Plugin.PatchFailures).Append(" failed");
                foreach (var e in Entries.Values) if (!e.Found) b.Append('\n').Append(e.Description);
                lock (Plugin.FailedPatches) foreach (var failure in Plugin.FailedPatches) b.Append("\nPatch failed: ").Append(failure);
                _late = log;
            }
            log(b.ToString());
        }

        public static string Json()
        {
            var b = new StringBuilder();
            lock (Gate)
            {
                int resolved = 0; foreach (var e in Entries.Values) if (e.Found) resolved++;
                b.Append("{\"resolved\":").Append(resolved).Append(",\"missing\":[");
                bool comma = false;
                foreach (var e in Entries.Values)
                {
                    if (e.Found) continue;
                    if (comma) b.Append(','); comma = true;
                    b.Append("{\"lookupKind\":").Append(TelemetryPrivacy.LookupStr("lookupKind", e.Kind)).Append(",\"declaringType\":").Append(TelemetryPrivacy.LookupStr("declaringType", e.Type))
                     .Append(",\"memberName\":").Append(TelemetryPrivacy.LookupStr("memberName", e.Member)).Append(",\"parameterTypes\":").Append(TelemetryPrivacy.LookupStr("parameterTypes", e.Parameters))
                     .Append(",\"sourceFile\":").Append(TelemetryPrivacy.LookupStr("sourceFile", e.File)).Append(",\"line\":").Append(e.Line).Append('}');
                }
                b.Append("],\"patchFailures\":["); comma = false;
                lock (Plugin.FailedPatches) foreach (var failure in Plugin.FailedPatches) { if (comma) b.Append(','); comma = true; b.Append(TelemetryPrivacy.LookupStr("patchFailures", failure)); }
                return b.Append("]}").ToString();
            }
        }
    }
}
