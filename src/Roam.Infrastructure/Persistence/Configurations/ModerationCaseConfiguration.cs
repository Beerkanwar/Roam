using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roam.Domain.TrustAndSafety;

namespace Roam.Infrastructure.Persistence.Configurations;

public class ModerationCaseConfiguration : IEntityTypeConfiguration<ModerationCase>
{
    public void Configure(EntityTypeBuilder<ModerationCase> builder)
    {
        builder.HasKey(m => m.Id);
        
        builder.HasIndex(m => m.TargetUserId);
        builder.HasIndex(m => m.Status);
        
        builder.HasOne<Roam.Domain.Users.ApplicationUser>()
            .WithMany()
            .HasForeignKey(m => m.TargetUserId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.HasOne<Report>()
            .WithMany()
            .HasForeignKey(m => m.ReportId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
