using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roam.Domain.Places;

namespace Roam.Infrastructure.Persistence.Configurations;

public class UserLocationConfiguration : IEntityTypeConfiguration<UserLocation>
{
    public void Configure(EntityTypeBuilder<UserLocation> builder)
    {
        builder.HasKey(u => u.Id);
        
        builder.HasIndex(u => u.UserId).IsUnique();
        builder.HasIndex(u => u.Location).HasMethod("gist");

        builder.HasOne<Roam.Domain.Users.ApplicationUser>()
            .WithMany()
            .HasForeignKey(u => u.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
