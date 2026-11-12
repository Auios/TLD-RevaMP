using UnityEngine;

namespace TLDRevamp
{
    internal static class DevOverlay
    {
        private static readonly PacketTable Packets = new PacketTable();
        private static readonly CompactOverlay Notice = new CompactOverlay();
        private static float _nextPackets, _width;
        private static int _revision = -1, _screenWidth;
        internal static void Install()
        {
            Overlay.LineFilter = DebugV2.Line;
            Overlay.Hidden = () => DebugV2.Hidden;
            Overlay.Measure += Measure;
            Overlay.AfterArrange += AfterArrange;
            Overlay.DrawExtras += Draw;
            Overlay.DrawCustom += DebugV2.DrawCustom;
        }
        private static void Measure(float width, ref DebugLayout.Request request)
        {
            float now = Time.realtimeSinceStartup;
            if (_width != width || _revision != OverlayStyle.Revision || _screenWidth != Screen.width)
            { _width = width; _revision = OverlayStyle.Revision; _screenWidth = Screen.width; _nextPackets = 0; }
            if (Net.NetMeter.Show && now >= _nextPackets)
            { _nextPackets = now + .5f; Packets.Rebuild(width, Net.Mp.Client != null || Net.Mp.Server != null); }
            Notice.Measure(Net.ItemTraffic.Show ? "RevaMP · Item traffic" : "RevaMP · Debug view: Everything", "Not in a multiplayer game");
            NetworkConditions.Measure(); WorldOverlays.MeasureMore(); ObjectPanels.ReserveLayout(width);
            bool notice = DebugView.Active && !Net.Entities.InSession && (WorldOverlays.Ownership || WorldOverlays.Ghost || WorldOverlays.Links || Net.ItemTraffic.Show);
            request.Packets = Net.NetMeter.Show && !DebugV2.Hidden ? Packets.Height : 0;
            request.Card = ObjectPanels.CardHeight; request.Compare = ObjectPanels.CompareHeight;
            request.Network = NetworkConditions.BarHeight; request.NetworkWidth = NetworkConditions.BarWidth;
            request.More = WorldOverlays.MoreHeight; request.MoreWidth = WorldOverlays.MoreWidth;
            request.Notice = notice ? Notice.Height : 0; request.NoticeWidth = Notice.Width;
            request.Banner = Net.Banner.Measure();
        }
        private static void AfterArrange()
        {
            DebugV2.AfterArrange();
            if (ItemHealth.Open && ItemHealth.Box.width > 0) DebugLayout.Labels.Reserve(new DebugBox(ItemHealth.Box.x, ItemHealth.Box.y, ItemHealth.Box.width, ItemHealth.Box.height));
        }
        private static void Draw()
        {
            Notice.Draw(DebugLayout.Notice, OverlayKind.Muted);
            WorldOverlays.Draw(); ItemTrafficLabels.Draw(); AttachBoxes.Draw(); Packets.Draw(DebugLayout.Packets);
            if (Plugin.Instance.Overlay.ShowWip) WipMark.Draw();
        }
    }
}
