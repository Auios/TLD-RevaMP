using System;
using System.Collections.Generic;
using UnityEngine;

public partial class tosaveitemscript { public List<RepairCondition> partconditions; }
public sealed class RepairCondition { public int state, state2, state3; }

namespace RepairFakes
{
    internal sealed class Menu
    {
        internal static Menu s;
        internal Map currentMainMap;
    }
    internal sealed class Map { internal List<Generator> poiGens = new List<Generator>(); }
    internal sealed class Generator
    {
        internal float lodDistance, removeDistance;
        internal Dictionary<int, Chunk> chunks = new Dictionary<int, Chunk>();
    }
    internal sealed class Chunk { internal Dictionary<int, Poi> pois = new Dictionary<int, Poi>(); }
    internal sealed class Poi { internal Vector3d pos, posID; internal int id; internal Component pobj; }
    internal sealed class Main
    {
        internal static Main s;
        internal fpscontroller player;
        internal static Vector3d GlobalFromUnityPos(Vector3 p) => new Vector3d(p.x, p.y, p.z);
    }
    internal sealed class Settings
    {
        internal static Settings s;
        internal Values S;
        internal sealed class Values
        {
            internal float Draw;
            internal bool Distant;
            internal float GetDrawDistanceLerp() => Draw;
            internal bool GetDistantTerrain() => Distant;
        }
    }
    internal sealed class Database
    {
        internal static Database s;
        internal bool Throws;
        internal readonly Dictionary<int, GameObject> Buildings = new Dictionary<int, GameObject>();
        internal GameObject Building(int type) => Throws ? throw new Exception("fixture") : Buildings.TryGetValue(type, out var go) ? go : null;
    }
}
