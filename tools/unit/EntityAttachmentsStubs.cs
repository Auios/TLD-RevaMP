using System;
using System.Linq;
using UnityEngine;

namespace TLDRevamp.Net
{
    public static partial class Entities
    {
        internal const byte AttachReq = 62, PartOff = 26;
        internal static readonly System.Collections.Generic.List<(int Player, string Notice)> AttachmentNotices = new System.Collections.Generic.List<(int, string)>();
        internal static void SendAttach(Ent entity, attachablescript at, byte type = AttachSync) => AttachmentTestTrace += "send:" + type + ";";
        internal static string AttachmentTestTrace = "";
        internal static void LiveUsablesChanged(tosaveitemscript item) => AttachmentTestTrace += "live;";
    }
}

namespace AttachmentFakes
{
    internal static partial class WireKernel
    {
        internal sealed class Ent
        {
            internal uint NetId, Epoch;
            internal tosaveitemscript Root;
            internal readonly System.Collections.Generic.List<tosaveitemscript> Items = new System.Collections.Generic.List<tosaveitemscript>();
            internal readonly System.Collections.Generic.HashSet<uint> HitchAnnounced = new System.Collections.Generic.HashSet<uint>();
        }
        private const byte AttachSync = 41, AttachReq = 62;
        private static readonly TLDRevamp.Net.NetWriter W = new TLDRevamp.Net.NetWriter();
        internal static save_attachable Record;
        internal static tosaveitemscript Parent;
        internal static Ent[] Group;
        internal static string AttachFails = "";
        internal static long AttachesFailed, AttachReqsSent, AttachesSent;
        private static int MyId = 2;
        private static void WireEvent(string kind, uint net, uint other, int item, int slot, int from, uint epoch) { }
        private static Ent[] LookupSet() => Group;
        private static void ToServer(TLDRevamp.Net.NetWriter writer, bool reliable) => Packets.Add(reliable + ":" + Convert.ToHexString(writer.Buf.AsSpan(0, writer.Len)));
        internal static readonly System.Collections.Generic.List<string> Packets = new System.Collections.Generic.List<string>();
        internal static void Send(Ent entity, attachablescript at, byte type) => SendAttach(entity, at, type);
    }
}
