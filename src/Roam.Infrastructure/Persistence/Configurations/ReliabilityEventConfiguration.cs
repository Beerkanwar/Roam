using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roam.Domain.TrustAndSafety;
using Roam.Domain.Users;

namespace Roam.Infrastructure.Persistence.Configurations;

public class ReliabilityEventConfiguration : IEntityTypeConfiguration<ReliabilityEvent>
{
    public void Configure(EntityTypeBuilder<ReliabilityEvent> builder)
    {
        builder.HasKey(e => e.Id);

        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => e.RelatedSessionId);
        builder.HasIndex(e => e.CreatedAt);
        builder.HasIndex(e => e.Type);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
