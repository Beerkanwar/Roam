using Microsoft.EntityFrameworkCore;
using Roam.Infrastructure.Persistence;

namespace Roam.Infrastructure.Scaling;

/// <summary>
/// Cross-server mutual exclusion using PostgreSQL session-level advisory locks, so work that must run
/// once (background sweeps, startup seeding) is not duplicated when several API instances run.
/// The DbContext's connection is held open for the duration, so every query the action issues through
/// the same context runs on the connection that owns the lock.
/// </summary>
public static class PostgresAdvisoryLock
{
    /// <summary>Lock id for the tour-request expiration / stale-session sweep.</summary>
    public const long ExpirationWorkerLockId = 0x524F414D_0001; // "ROAM" + 1
    /// <summary>Lock id for applying EF Core migrations at startup.</summary>
    public const long MigrationLockId = 0x524F414D_0003;
    /// <summary>Lock id for startup role/admin seeding.</summary>
    public const long SeedingLockId = 0x524F414D_0002;

    /// <summary>Runs <paramref name="action"/> only if no other instance holds the lock; returns whether it ran.</summary>
    public static async Task<bool> TryRunExclusiveAsync(ApplicationDbContext db, long lockId, Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var acquired = (await db.Database
                .SqlQuery<bool>($"SELECT pg_try_advisory_lock({lockId}) AS \"Value\"")
                .ToListAsync(cancellationToken)).Single();
            if (!acquired) return false;

            try
            {
                await action(cancellationToken);
                return true;
            }
            finally
            {
                await ReleaseAsync(db, lockId);
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    /// <summary>Waits until the lock is free, then runs <paramref name="action"/> while holding it.</summary>
    public static async Task RunExclusiveAsync(ApplicationDbContext db, long lockId, Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_lock({lockId})", cancellationToken);
            try
            {
                await action(cancellationToken);
            }
            finally
            {
                await ReleaseAsync(db, lockId);
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task ReleaseAsync(ApplicationDbContext db, long lockId)
    {
        // Use CancellationToken.None: the lock must be released even if the caller is shutting down.
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_unlock({lockId})", CancellationToken.None);
    }
}
