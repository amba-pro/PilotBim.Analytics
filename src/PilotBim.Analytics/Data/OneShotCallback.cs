using System;
using System.Threading;

namespace PilotBim.Analytics.Data
{
    /// <summary>
    /// Ensures a completion callback runs at most once (success or error).
    /// </summary>
    internal static class OneShotCallback
    {
        public static Action Wrap(Action action)
        {
            var fired = 0;
            return () =>
            {
                if (Interlocked.Exchange(ref fired, 1) != 0)
                    return;
                if (action != null)
                    action();
            };
        }

        public static Action<T> Wrap<T>(Action<T> action)
        {
            var fired = 0;
            return arg =>
            {
                if (Interlocked.Exchange(ref fired, 1) != 0)
                    return;
                if (action != null)
                    action(arg);
            };
        }
    }
}
