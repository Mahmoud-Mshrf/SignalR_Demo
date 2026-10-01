using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SignalR_Demo.Models.Chat_Models;

namespace SignalR_Demo.Data.Configurations;

public sealed class GroupParticipantConfiguration : IEntityTypeConfiguration<GroupParticipant>
{
    public void Configure(EntityTypeBuilder<GroupParticipant> builder)
    {
        builder.ToTable("GroupParticipants");

        builder.HasKey(x => new
        {
            x.GroupId,
            x.UserId
        });

        builder.HasOne(x => x.Group)
            .WithMany(x => x.Participants)
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.LastReadAt)
            .IsRequired();

        builder.Property(x => x.IsAdmin)
            .IsRequired();
    }
}