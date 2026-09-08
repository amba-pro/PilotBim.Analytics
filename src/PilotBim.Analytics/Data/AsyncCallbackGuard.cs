using System;
using System.Threading;

namespace PilotBim.Analytics.Data
{
    internal enum CallbackWaitStatus
    {
        Completed = 0,
        TimedOut = 1,
        Failed = 2,
        Cancelled = 3
    }

    internal sealed class CallbackWaitResult
    {
        public CallbackWaitResult(CallbackWaitStatus status, Exception error = null)
        {
            Status = status;
            Error = error;
        }

        public CallbackWaitStatus Status { get; private set; }
        public Exception Error { get; private set; }
        public bool Succeeded { get { return Status == CallbackWaitStatus.Completed; } }
    }

    /// <summary>
    /// One SDK wait round-trip: ManualResetEventSlim + abandon flag + distinguishable Wait result.
    /// Dispose only after Wait returns. Late callbacks must check <see cref="ShouldAccept"/> before mutating.
    /// Pilot SDK subscriptions are not cancellable here — timeout means abandon, not stop.
    /// </summary>
    internal sealed class CallbackWaitSession : IDisposable
    {
        private ManualResetEventSlim _gate = new ManualResetEventSlim(false);
        private int _abandoned;
        private int _disposed;
        private Exception _error;

        public bool ShouldAccept()
        {
            return AsyncCallbackGuard.ShouldAccept(Volatile.Read(ref _abandoned))
                && Volatile.Read(ref _disposed) == 0;
        }

        public bool IsAbandoned
        {
            get { return !AsyncCallbackGuard.ShouldAccept(Volatile.Read(ref _abandoned)); }
        }

        public void SignalCompleted()
        {
            TrySetGate();
        }

        public void SignalFailed(Exception error)
        {
            if (error != null)
                Interlocked.CompareExchange(ref _error, error, null);
            TrySetGate();
        }

        public CallbackWaitResult Wait(TimeSpan timeout)
        {
            return Wait(timeout, CancellationToken.None);
        }

        public CallbackWaitResult Wait(TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Abandon();
                return new CallbackWaitResult(CallbackWaitStatus.Cancelled);
            }

            bool signaled;
            try
            {
                signaled = _gate != null && _gate.Wait(timeout, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Abandon();
                return new CallbackWaitResult(CallbackWaitStatus.Cancelled);
            }
            catch (ObjectDisposedException)
            {
                Abandon();
                return new CallbackWaitResult(CallbackWaitStatus.Cancelled);
            }

            if (!signaled)
            {
                Abandon();
                return new CallbackWaitResult(CallbackWaitStatus.TimedOut);
            }

            var error = Volatile.Read(ref _error);
            if (error != null)
                return new CallbackWaitResult(CallbackWaitStatus.Failed, error);

            return new CallbackWaitResult(CallbackWaitStatus.Completed);
        }

        public void Abandon()
        {
            AsyncCallbackGuard.Abandon(ref _abandoned);
        }

        public void Dispose()
        {
            Abandon();
            Interlocked.Exchange(ref _disposed, 1);
            var gate = Interlocked.Exchange(ref _gate, null);
            if (gate == null)
                return;
            try
            {
                gate.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private void TrySetGate()
        {
            var gate = Volatile.Read(ref _gate);
            if (gate == null)
                return;
            try
            {
                gate.Set();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    /// <summary>
    /// Minimal post-timeout callback guard used by <see cref="CallbackWaitSession"/> and call sites.
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
