using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace TLDRevamp
{
    /// Shared cached type lookup and optional member aliases.
    public static class Reflect
    {
        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

        // Name → type table, built once. Scanning every assembly's types on each lookup cost 20–40 ms per bridge
        // command and caused most of the stutter/GC measured in the drive bench (2026-09-24).
        private static Dictionary<string, Type> _types;
        private static int _assemblyCount;

        private static readonly Dictionary<string, string> _memberAliases = new Dictionary<string, string>();

        /// Keep existing bridge paths when an optional plugin takes ownership of a member.
        internal static void RegisterMovedMembers(Type original, params Type[] owners)
        {
            foreach (var type in owners)
                foreach (var member in type.GetMembers(All | BindingFlags.DeclaredOnly))
                {
                    if (!(member is FieldInfo || member is PropertyInfo || member is MethodInfo) || original.GetMember(member.Name, All).Length != 0) continue;
                    _memberAliases[original.FullName + "." + member.Name] = type.FullName + "." + member.Name;
                    _memberAliases[original.Name + "." + member.Name] = type.FullName + "." + member.Name;
                }
        }

        internal static string MemberPath(string path)
        {
            for (int end = path.Length; end > 0; end = path.LastIndexOf('.', end - 1))
                if (_memberAliases.TryGetValue(path.Substring(0, end), out string target)) return target + path.Substring(end);
            return path;
        }

        public static Type FindType(string name)
        {
            var asms = AppDomain.CurrentDomain.GetAssemblies();
            if (_types == null || asms.Length != _assemblyCount)
            {
                var map = new Dictionary<string, Type>();
                foreach (var asm in asms)
                {
                    Type[] types;
                    try { types = asm.GetTypes(); } catch (ReflectionTypeLoadException e) { types = e.Types.Where(x => x != null).ToArray(); }
                    foreach (var t in types)
                    {
                        if (t.FullName != null && !map.ContainsKey(t.FullName)) map[t.FullName] = t;
                        if (!map.ContainsKey(t.Name)) map[t.Name] = t;  // short names: first wins, like before
                    }
                }
                _types = map;
                _assemblyCount = asms.Length;
            }
            return _types.TryGetValue(name, out var found) ? found : null;
        }

    }
}
