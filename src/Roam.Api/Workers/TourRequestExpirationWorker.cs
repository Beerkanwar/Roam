using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Roam.Domain.TourRequests;
using Roam.Infrastructure.Persistence;
using Roam.Infrastructure.Scaling;

namespace Roam.Api.Workers;

public class TourRequestExpirationWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TourRequestExpirationWorker> _logger;

    public TourRequestExpirationWorker(IServiceProvider serviceProvider, ILogger<TourRequestExpirationWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // With several API instances, only the one holding the advisory lock sweeps this cycle;
                // the others skip, so requests are not expired twice or notified twice.
                await PostgresAdvisoryLock.TryRunExclusiveAsync(
                    dbContext,
                    PostgresAdvisoryLock.ExpirationWorkerLockId,
                    ct => SweepAsync(scope.ServiceProvider, dbContext, ct),
                    stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during tour request expiration check.");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private async Task SweepAsync(IServiceProvider services, ApplicationDbContext dbContext, CancellationToken stoppingToken)
    {
        var now = DateTime.UtcNow;
        var expiredRequests = await dbContext.TourRequests
            .Where(r => (r.Status == TourRequestStatus.Published || r.Status == TourRequestStatus.VolunteerInterested)
                        && r.ExpiresAt.HasValue 
                        && r.ExpiresAt.Value < now)
            .OrderBy(r => r.ExpiresAt) // oldest first, so a backlog drains deterministically
            .Take(100)
            .ToListAsync(stoppingToken);

        if (expiredRequests.Count > 0)
        {
            var notificationService = services.GetRequiredService<Roam.Application.TourRequests.ITourNotificationService>();
            var successfullyExpired = new List<TourRequest>();

            foreach (var req in expiredRequests)
            {
                try
                {
                    req.TransitionTo(TourRequestStatus.Expired);
                    successfullyExpired.Add(req);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to transition tour request {RequestId} to Expired", req.Id);
                }
            }

            if (successfullyExpired.Count > 0)
            {
                await dbContext.SaveChangesAsync(stoppingToken);

                foreach (var req in successfullyExpired)
                {
                    try
                    {
                        await notificationService.TourRequestExpiredAsync(req.Id, req.RequesterId);
                    }
                    catch (Exception notifyEx)
                    {
                        _logger.LogWarning(notifyEx, "Failed to send expiration notification for request {RequestId}", req.Id);
                    }
                }

                _logger.LogInformation("Expired {Count} stale tour requests.", successfullyExpired.Count);
            }
        }

        // Check for abandoned sessions exceeding max duration (4 hours)
        var timeoutThreshold = now.AddHours(-4);
        var staleSessions = await dbContext.TourSessions
            .Where(s => (s.Status == Roam.Domain.TourSessions.SessionStatus.Active || s.Status == Roam.Domain.TourSessions.SessionStatus.Starting)
                        && s.StartedAt < timeoutThreshold)
            .OrderBy(s => s.StartedAt)
            .Take(50)
            .ToListAsync(stoppingToken);

        if (staleSessions.Count > 0)
        {
            var notificationService = services.GetRequiredService<Roam.Application.TourRequests.ITourNotificationService>();
            foreach (var session in staleSessions)
            {
                session.Status = Roam.Domain.TourSessions.SessionStatus.Completed;
                session.EndedAt = now;
                session.EndReason = "Session timed out after max duration";

                var tourReq = await dbContext.TourRequests.FindAsync(new object[] { session.TourRequestId }, stoppingToken);
                if (tourReq != null && tourReq.CanTransitionTo(TourRequestStatus.Completed))
                {
                    tourReq.TransitionTo(TourRequestStatus.Completed);
                }

                try
                {
                    await notificationService.TourEndedAsync(session.Id, session.HostVolunteerId, tourReq?.RequesterId ?? string.Empty);
                }
                catch (Exception notifyEx)
                {
                    _logger.LogWarning(notifyEx, "Failed to send tour ended notification for timed out session {SessionId}", session.Id);
                }
            }

            await dbContext.SaveChangesAsync(stoppingToken);
            _logger.LogInformation("Timed out {Count} abandoned tour sessions.", staleSessions.Count);
        }
    }
}
