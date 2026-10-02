using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roam.Domain.TrustAndSafety;

namespace Roam.Infrastructure.Persistence.Configurations;

public class ModerationAuditLogConfiguration : IEntityTypeConfiguration<ModerationAuditLog>
{
    public void Configure(EntityTypeBuilder<ModerationAuditLog> builder)
    {
        builder.HasKey(l => l.Id);
        
        builder.HasIndex(l => l.TargetUserId);
        builder.HasIndex(l => l.ModerationCaseId);
        
        builder.Property(l => l.Reason)
            .HasMaxLength(2000);
            
        builder.HasOne<Roam.Domain.Users.ApplicationUser>()
            .WithMany()
            .HasForeignKey(l => l.TargetUserId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.HasOne<Roam.Domain.Users.ApplicationUser>()
            .WithMany()
            .HasForeignKey(l => l.ModeratorId)
            .OnDelete(DeleteBehavior.SetNull);
            
        builder.HasOne<ModerationCase>()
            .WithMany()
            .HasForeignKey(l => l.ModerationCaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
