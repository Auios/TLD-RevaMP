namespace TLDRevamp.Net
{
    public static partial class JoinLoading
    {
        internal static bool ToggleStep(int step)
        {
            bool hasDetail = step >= 0 && step < 7 && (PreviewActive ? Preview.Details[step].Length > 0 : step < 6);
            if (!Showing || !Disclosure.ToggleStep(step, hasDetail)) return false;
            _nextText = 0;
            return true;
        }
        internal static bool ToggleReceived()
        {
            if (!Showing || (PreviewActive ? Preview.Question : MpScreen.PendingQuestion != null)) return false;
            Disclosure.ToggleReceived(); _nextText = 0;
            return true;
        }
    }
}
