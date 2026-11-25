using System.Diagnostics;
using System.Threading.RateLimiting;
using StackExchange.Redis;

namespace Roam.Api.Scaling;

/// <summary>
/// Fixed-window rate limiter whose counter lives in Redis, so a limit such as "10 logins per minute per IP"
/// holds across every API instance instead of being multiplied by the number of servers.
/// If Redis cannot be reached the limiter falls back to an in-process fixed window (fail-safe, not fail-open).
/// </summary>
public sealed class RedisFixedWindowRateLimiter : RateLimiter
{
    // INCRBY and set the window expiry atomically on the first hit of a window.
    private const string IncrementScript =
        "local c = redis.call('INCRBY', KEYS[1], ARGV[1]) " +
        "if c == tonumber(ARGV[1]) then redis.call('PEXPIRE', KEYS[1], ARGV[2]) end " +
        "return c";

    private readonly IConnectionMultiplexer _redis;
    private readonly RedisKey _key;
    private readonly int _permitLimit;
    private readonly TimeSpan _window;
    private readonly ILogger? _logger;
    private readonly FixedWindowRateLimiter _fallback;
    private long _lastUsedTimestamp = Stopwatch.GetTimestamp();

    public RedisFixedWindowRateLimiter(IConnectionMultiplexer redis, string key, int permitLimit, TimeSpan window, ILogger? logger = null)
    {
        _redis = redis;
        _key = key;
        _permitLimit = permitLimit;
        _window = window;
        _logger = logger;
        _fallback = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = window,
            QueueLimit = 0
        });
    }

    // Lets the partitioned limiter evict idle per-user/per-IP instances instead of keeping them forever.
    public override TimeSpan? IdleDuration => Stopwatch.GetElapsedTime(Interlocked.Read(ref _lastUsedTimestamp));

    public override RateLimiterStatistics? GetStatistics() => null;

    // The synchronous path cannot call Redis without blocking a thread; reporting "not acquired" makes the
    // rate-limiting middleware fall through to AcquireAsync, which does the real check.
    protected override RateLimitLease AttemptAcquireCore(int permitCount) => Lease.NotAcquiredWithoutRetryHint;

    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
    {
        Interlocked.Exchange(ref _lastUsedTimestamp, Stopwatch.GetTimestamp());
        if (permitCount > _permitLimit)
            return Lease.NotAcquiredWithoutRetryHint;

        try
        {
            var db = _redis.GetDatabase();
            var count = (long)await db.ScriptEvaluateAsync(
                IncrementScript,
                new[] { _key },
                new RedisValue[] { permitCount, (long)_window.TotalMilliseconds });

            if (count <= _permitLimit)
                return Lease.Acquired;

            var ttl = await db.KeyTimeToLiveAsync(_key);
            return new Lease(false, ttl ?? _window);
        }
        catch (Exception ex) when (ex is RedisException or RedisTimeoutException or RedisConnectionException or ObjectDisposedException)
        {
            _logger?.LogWarning(ex, "Redis rate limiter unavailable; falling back to the per-instance limiter.");
            return await _fallback.AcquireAsync(permitCount, cancellationToken);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _fallback.Dispose();
        base.Dispose(disposing);
    }

    private sealed class Lease : RateLimitLease
    {
        public static readonly Lease Acquired = new(true, null);
        public static readonly Lease NotAcquiredWithoutRetryHint = new(false, null);

        private readonly TimeSpan? _retryAfter;

        public Lease(bool isAcquired, TimeSpan? retryAfter)
        {
            IsAcquired = isAcquired;
            _retryAfter = retryAfter;
        }

        public override bool IsAcquired { get; }

        public override IEnumerable<string> MetadataNames =>
            _retryAfter.HasValue ? new[] { MetadataName.RetryAfter.Name } : Array.Empty<string>();

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            if (_retryAfter.HasValue && metadataName == MetadataName.RetryAfter.Name)
            {
                metadata = _retryAfter.Value;
                return true;
            }
            metadata = null;
            return false;
        }
    }
}
