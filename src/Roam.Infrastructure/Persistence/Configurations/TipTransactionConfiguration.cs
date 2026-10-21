using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roam.Domain.Payments;
using Roam.Domain.TourSessions;
using Roam.Domain.Users;

namespace Roam.Infrastructure.Persistence.Configurations;

public class TipTransactionConfiguration : IEntityTypeConfiguration<TipTransaction>
{
    public void Configure(EntityTypeBuilder<TipTransaction> builder)
    {
        builder.HasKey(t => t.Id);

        builder.HasIndex(t => t.SenderId);
        builder.HasIndex(t => t.ReceiverId);
        builder.HasIndex(t => t.TourSessionId);
        builder.HasIndex(t => t.IdempotencyKey);
        builder.HasIndex(t => t.CreatedAt);

        builder.Property(t => t.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(t => t.Amount)
            .HasPrecision(18, 2);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(t => t.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(t => t.ReceiverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<TourSession>()
            .WithMany()
            .HasForeignKey(t => t.TourSessionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
