using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roam.Domain.TourSessions;
using Roam.Domain.Users;

namespace Roam.Infrastructure.Persistence.Configurations;

public class TourParticipantConfiguration : IEntityTypeConfiguration<TourParticipant>
{
    public void Configure(EntityTypeBuilder<TourParticipant> builder)
    {
        builder.HasKey(p => p.Id);

        builder.HasIndex(p => new { p.TourSessionId, p.UserId }).IsUnique();
        builder.HasIndex(p => p.UserId);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
