using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roam.Domain.TourSessions;
using Roam.Domain.TrustAndSafety;
using Roam.Domain.Users;

namespace Roam.Infrastructure.Persistence.Configurations;

public class FeedbackConfiguration : IEntityTypeConfiguration<Feedback>
{
    public void Configure(EntityTypeBuilder<Feedback> builder)
    {
        builder.HasKey(f => f.Id);

        builder.HasIndex(f => new { f.TourSessionId, f.ReviewerId, f.TargetUserId }).IsUnique();
        builder.HasIndex(f => f.TargetUserId);
        builder.HasIndex(f => f.CreatedAt);

        builder.Property(f => f.Comments)
            .HasMaxLength(2000);

        builder.HasOne<TourSession>()
            .WithMany()
            .HasForeignKey(f => f.TourSessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(f => f.ReviewerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(f => f.TargetUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
