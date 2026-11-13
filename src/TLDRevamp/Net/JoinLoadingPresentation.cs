using System.Collections.Generic;

namespace TLDRevamp.Net
{
    internal interface IJoinLoadingPreview
    {
        bool Active { get; }
        int Stage { get; }
        bool Question { get; }
        bool Stuck { get; }
        int Expected { get; }
        int Applied { get; }
        int Received { get; }
        string Host { get; }
        string QuestionText { get; }
        IReadOnlyList<string> Names { get; }
        IReadOnlyList<string> Details { get; }
    }
    public static partial class JoinLoading
    {
        private static IJoinLoadingPreview Preview => DevHooks.JoinPreview?.Invoke();
        private static bool PreviewActive => Preview?.Active ?? false;
        internal static void ResetPresentation(bool expanded)
        { Disclosure.Reset(expanded); _nextText = 0; _outerScroll = UnityEngine.Vector2.zero; }
    }
}
