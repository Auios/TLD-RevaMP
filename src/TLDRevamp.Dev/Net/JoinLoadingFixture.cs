namespace TLDRevamp.Net
{
    // Presentation-only fixture. The bridge is the sole entry point; never borrow live state or metrics.
    internal static class JoinLoadingPreview
    {
        internal static readonly IJoinLoadingPreview View = new PreviewView();
        private sealed class PreviewView : IJoinLoadingPreview
        {
            public bool Active => JoinLoadingPreview.Active;
            public int Stage => JoinLoadingPreview.Stage;
            public bool Question => JoinLoadingPreview.Question;
            public bool Stuck => JoinLoadingPreview.Stuck;
            public int Expected => JoinLoadingPreview.Expected;
            public int Applied => JoinLoadingPreview.Applied;
            public int Received => JoinLoadingPreview.Received;
            public string Host => JoinLoadingPreview.Host;
            public string QuestionText => JoinLoadingPreview.QuestionText;
            public System.Collections.Generic.IReadOnlyList<string> Names => JoinLoadingPreview.Names;
            public System.Collections.Generic.IReadOnlyList<string> Details => JoinLoadingPreview.Details;
        }
        internal static void Install() { DevHooks.JoinPreview = () => View; DevHooks.JoinPreviewEnd = Off; }
        internal static bool Active { get; private set; }
        internal static int Stage { get; private set; }
        internal static bool Question { get; private set; }
        internal static bool Stuck { get; private set; }
        internal static bool Expanded { get; private set; }
        internal const int Expected = 342, Applied = 128, Received = 154;
        internal const string Host = "Sample host", QuestionText = "SAMPLE PREVIEW · This host uses Example mod 1.2 (2.41 MB). Download the required mods to join? Nothing is downloaded in preview.";
        internal static readonly string[] Names = { "Car04", "Beer", "Box", "Barrel", "Radio02", "Jerrycan" };
        internal static readonly string[] Details = {
            "Sample host · direct · 34 ms", "RevaMP version accepted; mods matched",
            "Local scene loaded · 97 B from host", "Terrain 46 / 64 tiles · Roads 12 / 12 chunks",
            "Applied 128 / 342 · received 154 / 342 · waiting for initial records",
            "Waiting for settled ground", "Sample world ready" };
        internal static void Off() => Active = Question = Stuck = Expanded = false;
        internal static bool Command(string[] args)
        {
            if (args.Length == 3 && args[2] == "off") { Off(); return true; }
            if (args.Length < 3 || args.Length > 5 || !int.TryParse(args[2], out int stage) || stage < 0 || stage > 6) return false;
            int next = 3;
            bool question = next < args.Length && args[next] == "question", stuck = next < args.Length && args[next] == "stuck";
            if (question || stuck) next++;
            bool expanded = next < args.Length && args[next] == "expanded";
            if (expanded) next++;
            if (next != args.Length) return false;
            Active = true; Stage = stage;
            Question = question; Stuck = stuck; Expanded = expanded;
            return true;
        }
    }
}
