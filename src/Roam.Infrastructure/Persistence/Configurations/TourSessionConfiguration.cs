using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roam.Domain.TourRequests;
using Roam.Domain.TourSessions;
using Roam.Domain.Users;

namespace Roam.Infrastructure.Persistence.Configurations;

public class TourSessionConfiguration : IEntityTypeConfiguration<TourSession>
{
    public void Configure(EntityTypeBuilder<TourSession> builder)
    {
        builder.HasKey(s => s.Id);

        builder.HasIndex(s => s.TourRequestId);
        builder.HasIndex(s => s.HostVolunteerId);
        builder.HasIndex(s => s.Status);
        builder.HasIndex(s => s.StartedAt);

        builder.HasOne<TourRequest>()
            .WithMany()
            .HasForeignKey(s => s.TourRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(s => s.HostVolunteerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.Participants)
            .WithOne()
            .HasForeignKey(p => p.TourSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
