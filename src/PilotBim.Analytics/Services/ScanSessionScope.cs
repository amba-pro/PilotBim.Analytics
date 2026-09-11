using System;
using System.Threading;

namespace PilotBim.Analytics.Services
{
    /// <summary>
    /// Owns the active scan <see cref="CancellationTokenSource"/> for a window.
    /// Ensures previous sessions are cancelled/disposed before a new begin,
    /// and that completing a superseded session cannot dispose the newer one.
    /// </summary>
    internal sealed class ScanSessionScope : IDisposable
    {
        private CancellationTokenSource _cts;
        private int _version;
        private bool _disposed;

        public bool IsActive
        {
            get { return _cts != null && !_disposed; }
        }

        /// <summary>
        /// Cancels and disposes any previous source, then starts a new session.
        /// </summary>
        public ScanSession Begin()
        {
            ThrowIfDisposed();
            CancelAndDisposeCurrent();
            _cts = new CancellationTokenSource();
            _version++;
            return new ScanSession(_version, _cts.Token);
        }

        /// <summary>
        /// Cancels the active token if any. Safe after Complete/Dispose.
        /// </summary>
        public void Cancel()
        {
            if (_disposed)
                return;
            var cts = _cts;
            if (cts == null)
                return;
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        /// <summary>
        /// Disposes the CTS for <paramref name="session"/> only if it is still the active session.
        /// A superseded (older) session completion is a no-op.
        /// </summary>
        public void Complete(ScanSession session)
        {
            if (_disposed)
                return;
            if (session.Version != _version)
                return;
            CancelAndDisposeCurrent();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            CancelAndDisposeCurrent();
            _disposed = true;
        }

        private void CancelAndDisposeCurrent()
        {
            var cts = _cts;
            _cts = null;
            if (cts == null)
                return;
            try
            {
                if (!cts.IsCancellationRequested)
                    cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
            cts.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ScanSessionScope));
        }
    }

    internal struct ScanSession
    {
        public ScanSession(int version, CancellationToken token)
        {
            Version = version;
            Token = token;
        }

        public int Version { get; private set; }

        public CancellationToken Token { get; private set; }
    }
}
