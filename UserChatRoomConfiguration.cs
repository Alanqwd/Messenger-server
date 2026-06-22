using Messenger_server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Messenger_server.Data.Configurations
{
    public class UserChatRoomConfiguration : IEntityTypeConfiguration<UserChatRoom>
    {
        public void Configure(EntityTypeBuilder<UserChatRoom> builder)
        {

            builder.HasKey(ucr => new { ucr.UserId, ucr.ChatRoomId });

          
            builder.HasOne(ucr => ucr.User)
                .WithMany()
                .HasForeignKey(ucr => ucr.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(ucr => ucr.ChatRoom)
                .WithMany(cr => cr.UserChatRooms)
                .HasForeignKey(ucr => ucr.ChatRoomId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}