using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

using Roam.Domain.Places;
using Roam.Domain.TourRequests;
using Roam.Domain.TourSessions;
using Roam.Domain.TrustAndSafety;
using Roam.Domain.Users;
using Roam.Domain.Chat;
using Roam.Domain.Payments;

namespace Roam.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public DbSet<Place> Places { get; set; }
    public DbSet<TourRequest> TourRequests { get; set; }
    public DbSet<ScheduledTour> ScheduledTours { get; set; }
    public DbSet<TourSession> TourSessions { get; set; }
    public DbSet<TourParticipant> TourParticipants { get; set; }
    public DbSet<Report> Reports { get; set; }
    public DbSet<ReliabilityEvent> ReliabilityEvents { get; set; }
    public DbSet<VolunteerSettings> VolunteerSettings { get; set; }
    public DbSet<UserLocation> UserLocations { get; set; }
    public DbSet<UserBlock> UserBlocks { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<ModerationCase> ModerationCases { get; set; }
    public DbSet<ModerationAuditLog> ModerationAuditLogs { get; set; }

    // Payments & Feedback
    public DbSet<Feedback> Feedbacks { get; set; }
    public DbSet<TipAccount> TipAccounts { get; set; }
    public DbSet<TipTransaction> TipTransactions { get; set; }
    public DbSet<Donation> Donations { get; set; }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        
        // PostGIS setup
        builder.HasPostgresExtension("postgis");
        
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
