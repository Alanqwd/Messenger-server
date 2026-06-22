using System.ComponentModel.DataAnnotations.Schema;

namespace Messenger_server.Models
{
    [Table("UserChatRooms")]
    public class UserChatRoom
    {
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public int ChatRoomId { get; set; }
        public ChatRoom ChatRoom { get; set; } = null!;

        public int UnreadCount { get; set; } = 0;
    }
}