using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roam.Domain.Users;

namespace Roam.Infrastructure.Persistence.Configurations;

public class UserBlockConfiguration : IEntityTypeConfiguration<UserBlock>
{
    public void Configure(EntityTypeBuilder<UserBlock> builder)
    {
        builder.HasKey(ub => new { ub.BlockerId, ub.BlockedId });
        
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(ub => ub.BlockerId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(ub => ub.BlockedId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
