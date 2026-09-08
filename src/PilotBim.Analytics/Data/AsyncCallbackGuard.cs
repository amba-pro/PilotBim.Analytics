using System.Threading;

namespace PilotBim.Analytics.Data
{
    /// <summary>
    /// Minimal post-timeout callback guard. Full ManualResetEventSlim helper remains Stage 4.
    /// </summary>
    internal static class AsyncCallbackGuard
    {
        public static bool ShouldAccept(int abandonedFlag)
        {
            return abandonedFlag == 0;
        }

        public static void Abandon(ref int abandonedFlag)
        {
            Interlocked.Exchange(ref abandonedFlag, 1);
        }
    }
}
