using System;
using UnityEngine;

namespace TLDRevamp
{
    /// Page Up (Plugin.KeyOverlay): frametime graph + stats. Page Down (Plugin.KeyReport): capture a debug report.
    ///
    /// Cheap by design (measured 2026-09-25: the first version cost ~0.5 ms/frame, 240 GUI.DrawTexture calls plus string
    /// formatting and two percentile sorts on every OnGUI event): the graph is one 240×70 texture rewritten 10×/s, the
    /// text is rebuilt 4×/s, only the Repaint event draws, and the Runner skips IMGUI's layout pass (useGUILayout off).
    public class Overlay
    {
        public bool Visible = true;
        public bool ShowWip = true;

        private const int GW = 240, GH = 70;
        private readonly Telemetry _t;
        private readonly float[] _hist = new float[GW];
        private readonly Color32[] _pixels = new Color32[GW * GH];
        private Texture2D _graph;
        private string _toast, _text = "";
        private float _toastUntil, _nextGraph, _nextText;
        private readonly CompactOverlay _toastBar = new CompactOverlay();
        internal static Func<string, string, bool> LineFilter;
        internal static Func<bool> Hidden;
        internal delegate void LayoutContributor(float width, ref DebugLayout.Request request);
        internal static LayoutContributor Measure;
        internal static System.Action AfterArrange, DrawExtras, DrawCustom;
        private static bool Line(string panel, string line) => LineFilter?.Invoke(panel, line) ?? true;
        private static bool IsHidden => Hidden?.Invoke() ?? false;
        private int _styleRevision = -1;
        private float _textHeight;
        private float _panelWidth; private int _screenWidth;
        private readonly GUIContent _content = new GUIContent();

        private static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        public Overlay(Telemetry t) => _t = t;

        public void Toast(string msg, float seconds = 4f)
        {
            _toast = msg;
            _toastUntil = Time.realtimeSinceStartup + seconds; _nextToast = 0;
        }

        public void Draw()
        {
            if (Event.current.type != EventType.Repaint) return;
            OverlayStyle.Ensure();
            if (_graph == null) _graph = new Texture2D(GW, GH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            float width = DebugLayout.LeftWidth(Screen.width, Screen.height, MarkPanel.Opened);
            if (_styleRevision != OverlayStyle.Revision || _panelWidth != width || _screenWidth != Screen.width)
            {
                _styleRevision = OverlayStyle.Revision; _panelWidth = width; _screenWidth = Screen.width; _nextText = _nextToast = 0;
            }
            float now = Time.realtimeSinceStartup, scale = OverlayStyle.Scale;
            if (Visible && now >= _nextText)
            {
                _nextText = now + .25f;
                float p50 = _t.Percentile(.5f);
                _t.Lows(out float low1, out float low01);
                // each line can be switched off in Debugging V2 (DebugV2.Line: always on in Legacy)
                var lines = new System.Collections.Generic.List<string>(6);
                if (Line("perf", "fps")) lines.Add($"{Plugin.Version} ({Plugin.CommitLabel})  {_t.Fps:F0} fps - {_t.AvgFps:F0} avg - {low1:F0} 1% low - {low01:F0} 0.1% low");
                if (Line("perf", "times")) lines.Add($"p50  {p50:F1} ms     p99  {_t.Percentile(.99f):F1} ms");
                if (Line("perf", "stats")) lines.Add($"Hitches  {_t.Hitches}     GC  {_t.GcCollections}     Heap  {_t.ManagedMB} MB");
                if (Line("perf", "keys")) lines.Add($"{Plugin.KeyName(Plugin.KeyOverlay)} hide · {Plugin.KeyName(Plugin.KeyReport)} report");
                string net = null; try { net = Net.Mp.NetStatsLine(); } catch (System.Exception quietError) { Diagnostics.QuietErrors.Once("Overlay.NetStats", quietError); }
                if (net != null)
                {
                    var parts = net.Replace("<b>", "").Replace("</b>", "").Replace("  down", "\nDown").Split('\n');
                    if (Line("perf", "mp")) lines.Add(parts[0]);
                    if (parts.Length > 1 && Line("perf", "net")) lines.Add(parts[1]);
                }
                _text = string.Join("\n", lines);
                _content.text = OverlayStyle.Truncate(_text, Mathf.Max(0, width - 2 * OverlayStyle.Padding));
                _textHeight = OverlayStyle.Height(_content, width - 2 * OverlayStyle.Padding);
            }
            bool toast = _toast != null && now < _toastUntil;
            if (toast && now >= _nextToast)
            {
                _toastBar.Measure("RevaMP · Notice", _toast.Replace("\n", " · ")); _nextToast = now + .25f;
            }
            ReproRecorder.Measure();
            var request = new DebugLayout.Request {
                Performance = Visible && !IsHidden ? OverlayStyle.PanelHeight((Line("perf", "graph") ? 36 * scale : 0) + (_text.Length > 0 ? _textHeight : 0), OverlayTool.Performance) : 0,
                Toast = toast ? _toastBar.Height : 0, ToastWidth = _toastBar.Width,
                Recording = ReproRecorder.BarHeight, RecordingWidth = ReproRecorder.BarWidth,
                Mark = MarkPanel.Measure() };
            Measure?.Invoke(width, ref request);
            DebugLayout.Arrange(Screen.width, Screen.height, request);
            AfterArrange?.Invoke();
            _toastBar.Draw(DebugLayout.Toast);
            DrawExtras?.Invoke();
            if (DebugLayout.Performance.H > 0)
            {
                if (now >= _nextGraph) { _nextGraph = now + .1f; RedrawGraph(); }
                var content = OverlayStyle.Panel(DebugLayout.Performance, OverlayTool.Performance);
                float graph = Line("perf", "graph") ? 36 * scale : 0;
                if (graph > 0) { var old = GUI.color; GUI.color = Color.white; GUI.DrawTexture(new Rect(content.x, content.y, content.width, Mathf.Min(32 * scale, content.height)), _graph); GUI.color = old; }
                OverlayStyle.Label(new Rect(content.x, content.y + graph, content.width, Mathf.Max(0, content.height - graph)), _content);
            }
            DrawCustom?.Invoke();
        }

        private float _nextToast;
        /// Frametime graph, adaptive range up to 50 ms, line at 16.7 ms; texture row 0 is the bottom.
        private void RedrawGraph()
        {
            int n = _t.CopyHistory(_hist);
            int start = GW - n; // newest at the right, as before
            float range = 16.7f;
            for (int i = 0; i < n; i++) range = Mathf.Max(range, Mathf.Min(50, _hist[i]));
            int lineY = Mathf.Min(GH - 1, Mathf.RoundToInt(16.7f / range * GH));
            for (int col = 0; col < GW; col++)
            {
                int bh = 0;
                Color32 c = OverlayStyle.Ok;
                if (col >= start)
                {
                    float ms = _hist[col - start];
                    c = ms > 50 ? OverlayStyle.Problem : ms > 16.7f ? OverlayStyle.Warning : OverlayStyle.Ok;
                    bh = Mathf.RoundToInt(Mathf.Min(ms / range, 1f) * GH);
                }
                for (int row = 0; row < GH; row++)
                    _pixels[row * GW + col] = row < bh ? c : row == lineY ? (Color32)OverlayStyle.Muted : Clear;
            }
            _graph.SetPixels32(_pixels);
            _graph.Apply(false);
        }
    }
}
