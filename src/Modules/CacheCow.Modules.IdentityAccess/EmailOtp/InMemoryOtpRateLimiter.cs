using System.Collections.Concurrent;

namespace CacheCow.Modules.IdentityAccess.EmailOtp;

/// <summary>
/// In-memory fixed-window <see cref="IOtpRateLimiter"/> for tests and
/// single-node composition. NOT a distributed limiter: multi-region
/// enforcement needs a shared-state implementation behind the same port
/// (a later issue).
/// </summary>
public sealed class InMemoryOtpRateLimiter : IOtpRateLimiter
{
    private sealed class Window
    {
        public DateTimeOffset Start { get; set; }

        public int Count { get; set; }
    }

    private readonly ConcurrentDictionary<string, Window> _windows = new();
    private readonly TimeProvider _timeProvider;

    public InMemoryOtpRateLimiter(TimeProvider? timeProvider = null) =>
        _timeProvider = timeProvider ?? TimeProvider.System;

    public OtpRateLimitDecision Acquire(string scopeKey, int limit, TimeSpan window)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeKey);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(window, TimeSpan.Zero);

        var now = _timeProvider.GetUtcNow();
        var state = _windows.GetOrAdd(scopeKey, _ => new Window { Start = now, Count = 0 });

        lock (state)
        {
            if (now >= state.Start + window)
            {
                state.Start = now;
                state.Count = 0;
            }

            state.Count++;

            return state.Count <= limit
                ? new OtpRateLimitDecision(true, TimeSpan.Zero)
                : new OtpRateLimitDecision(false, state.Start + window - now);
        }
    }
}
