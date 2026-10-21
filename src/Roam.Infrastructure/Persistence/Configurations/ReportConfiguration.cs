using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roam.Domain.TrustAndSafety;
using Roam.Domain.Users;

namespace Roam.Infrastructure.Persistence.Configurations;

public class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.HasKey(r => r.Id);

        builder.HasIndex(r => r.ReporterId);
        builder.HasIndex(r => r.ReportedUserId);
        builder.HasIndex(r => r.TourSessionId);
        builder.HasIndex(r => r.Status);
        builder.HasIndex(r => r.CreatedAt);

        builder.Property(r => r.Description)
            .HasMaxLength(2000);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(r => r.ReporterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(r => r.ReportedUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
