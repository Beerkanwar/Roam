using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roam.Domain.Places;
using Roam.Domain.TourRequests;
using Roam.Domain.Users;

namespace Roam.Infrastructure.Persistence.Configurations;

public class TourRequestConfiguration : IEntityTypeConfiguration<TourRequest>
{
    public void Configure(EntityTypeBuilder<TourRequest> builder)
    {
        builder.HasKey(r => r.Id);

        builder.HasIndex(r => r.RequesterId);
        builder.HasIndex(r => r.AssignedVolunteerId);
        builder.HasIndex(r => r.PlaceId);
        builder.HasIndex(r => new { r.Status, r.ExpiresAt });
        builder.HasIndex(r => r.CreatedAt);

        builder.Property(r => r.Description)
            .HasMaxLength(2000);

        builder.HasOne(r => r.Place)
            .WithMany()
            .HasForeignKey(r => r.PlaceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(r => r.RequesterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(r => r.AssignedVolunteerId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
