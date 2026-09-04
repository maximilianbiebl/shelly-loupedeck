using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ShellyLoupedeckPlugin
{
    /// <summary>
    /// Turns a burst of dial input into as few cloud writes as possible.
    ///
    /// Two limits apply per key (a device or group):
    ///  - a quiet period, so a continuous turn sends once it settles rather than
    ///    at every detent
    ///  - a minimum gap between writes, so a slow but steady turn - whose pauses
    ///    exceed the quiet period - still cannot outpace the API
    ///
    /// The value itself is not carried here; callers keep their own state and the
    /// send delegate reads whatever is current when it runs. A write requested
    /// while one is in flight is folded into a single follow-up.
    /// </summary>
    internal sealed class WriteScheduler : IDisposable
    {
        private sealed class Entry
        {
            public Timer Timer;
            public DateTime LastSent = DateTime.MinValue;
            public bool Sending;
            public bool DirtyWhileSending;
        }

        private readonly Func<string, Task> _send;
        private readonly int _quietPeriodMs;
        private readonly int _minIntervalMs;
        private readonly string _owner;

        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();
        private readonly object _lock = new object();
        private bool _disposed;

        public WriteScheduler(string owner, Func<string, Task> send, int quietPeriodMs = 500, int minIntervalMs = 1500)
        {
            _owner = owner;
            _send = send ?? throw new ArgumentNullException(nameof(send));
            _quietPeriodMs = quietPeriodMs;
            _minIntervalMs = minIntervalMs;
        }

        /// <summary>Requests a write for <paramref name="key"/> once input settles.</summary>
        public void Schedule(string key)
        {
            if (string.IsNullOrEmpty(key))
                return;

            lock (_lock)
            {
                if (_disposed)
                    return;

                if (!_entries.TryGetValue(key, out var entry))
                {
                    entry = new Entry();
                    _entries[key] = entry;
                }

                // A write is already running; fold this request into one follow-up
                if (entry.Sending)
                {
                    entry.DirtyWhileSending = true;
                    return;
                }

                Rearm(key, entry, _quietPeriodMs);
            }
        }

        /// <summary>Restarts the timer for a key. Called with the lock held.</summary>
        private void Rearm(string key, Entry entry, int dueMs)
        {
            entry.Timer?.Dispose();
            entry.Timer = new Timer(_ => OnDue(key), null, dueMs, Timeout.Infinite);
        }

        private void OnDue(string key)
        {
            lock (_lock)
            {
                if (_disposed || !_entries.TryGetValue(key, out var entry) || entry.Sending)
                    return;

                entry.Timer?.Dispose();
                entry.Timer = null;

                // Respect the minimum gap, waiting out the remainder if needed
                var sinceLast = (DateTime.UtcNow - entry.LastSent).TotalMilliseconds;
                if (sinceLast < _minIntervalMs)
                {
                    Rearm(key, entry, (int)Math.Ceiling(_minIntervalMs - sinceLast));
                    return;
                }

                entry.Sending = true;
                entry.DirtyWhileSending = false;
            }

            _ = RunSendAsync(key);
        }

        private async Task RunSendAsync(string key)
        {
            try
            {
                await _send(key);
            }
            catch (Exception ex)
            {
                DebugLogger.Warn($"[{_owner}] Scheduled write for {key} failed: {ex.Message}");
            }
            finally
            {
                var followUp = false;

                lock (_lock)
                {
                    if (_entries.TryGetValue(key, out var entry))
                    {
                        entry.Sending = false;
                        entry.LastSent = DateTime.UtcNow;
                        followUp = entry.DirtyWhileSending;
                        entry.DirtyWhileSending = false;
                    }
                }

                if (followUp)
                    Schedule(key);
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                    return;

                _disposed = true;

                foreach (var entry in _entries.Values)
                    entry.Timer?.Dispose();

                _entries.Clear();
            }
        }
    }
}
