using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SignalR_Demo.Models.Chat_Models;

namespace SignalR_Demo.Data.Configurations;

public sealed class ChatParticipantConfiguration 
    : IEntityTypeConfiguration<ChatParticipant>
{
    public void Configure(EntityTypeBuilder<ChatParticipant> builder)
    {
        builder.ToTable("ChatParticipants");

        builder.HasKey(x => new
        {
            x.ChatId,
            x.UserId
        });

        builder.HasOne(x => x.Chat)
            .WithMany(x => x.Participants)
            .HasForeignKey(x => x.ChatId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.LastReadAt)
            .IsRequired();
    }
}
