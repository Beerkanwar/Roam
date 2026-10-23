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

                var now = DateTime.UtcNow;
                var expiredRequests = await dbContext.TourRequests
                    .Where(r => (r.Status == TourRequestStatus.Published || r.Status == TourRequestStatus.VolunteerInterested)
                                && r.ExpiresAt.HasValue 
                                && r.ExpiresAt.Value < now)
                    .Take(100)
                    .ToListAsync(stoppingToken);

                if (expiredRequests.Count > 0)
                {
                    var notificationService = scope.ServiceProvider.GetRequiredService<Roam.Application.TourRequests.ITourNotificationService>();
                    foreach (var req in expiredRequests)
                    {
                        req.TransitionTo(TourRequestStatus.Expired);
                    }
                    await dbContext.SaveChangesAsync(stoppingToken);

                    foreach (var req in expiredRequests)
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

                    _logger.LogInformation("Expired {Count} stale tour requests.", expiredRequests.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during tour request expiration check.");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
