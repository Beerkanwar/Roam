using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roam.Domain.Payments;
using Roam.Domain.Users;

namespace Roam.Infrastructure.Persistence.Configurations;

public class DonationConfiguration : IEntityTypeConfiguration<Donation>
{
    public void Configure(EntityTypeBuilder<Donation> builder)
    {
        builder.HasKey(d => d.Id);

        builder.HasIndex(d => d.DonorId);
        builder.HasIndex(d => d.IdempotencyKey);
        builder.HasIndex(d => d.CreatedAt);

        builder.Property(d => d.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(d => d.Amount)
            .HasPrecision(18, 2);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(d => d.DonorId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
