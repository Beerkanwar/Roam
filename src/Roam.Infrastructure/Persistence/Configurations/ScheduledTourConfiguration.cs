using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roam.Domain.TourRequests;
using Roam.Domain.Users;

namespace Roam.Infrastructure.Persistence.Configurations;

public class ScheduledTourConfiguration : IEntityTypeConfiguration<ScheduledTour>
{
    public void Configure(EntityTypeBuilder<ScheduledTour> builder)
    {
        builder.HasKey(s => s.Id);

        builder.HasIndex(s => s.TourRequestId);
        builder.HasIndex(s => s.ProposedByUserId);
        builder.HasIndex(s => s.ProposedStartUtc);
        builder.HasIndex(s => s.Status);

        builder.HasOne<TourRequest>()
            .WithMany()
            .HasForeignKey(s => s.TourRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(s => s.ProposedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
