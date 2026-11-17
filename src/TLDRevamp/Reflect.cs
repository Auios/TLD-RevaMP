using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace TLDRevamp
{
    /// Shared cached type lookup.
    public static class Reflect
    {
        // Name → type table, built once. Scanning every assembly's types on each lookup cost 20–40 ms per bridge
        // command and caused most of the stutter/GC measured in the drive bench (2026-09-24).
        private static Dictionary<string, Type> _types;
        private static int _assemblyCount;

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
            return GameLookups.RuntimeFoundType(name, _types.TryGetValue(name, out var found) ? found : null);
        }

    }
}
