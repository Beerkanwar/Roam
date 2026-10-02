using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Roam.Domain.Chat;

namespace Roam.Infrastructure.Persistence.Configurations;

public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.HasKey(c => c.Id);
        
        builder.HasIndex(c => c.SessionId);
        builder.HasIndex(c => c.SentAt);
        
        builder.Property(c => c.Text)
            .IsRequired()
            .HasMaxLength(2000);
            
        builder.HasOne<Roam.Domain.TourSessions.TourSession>()
            .WithMany()
            .HasForeignKey(c => c.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.HasOne<Roam.Domain.Users.ApplicationUser>()
            .WithMany()
            .HasForeignKey(c => c.SenderId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
