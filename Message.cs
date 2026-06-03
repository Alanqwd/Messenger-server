using System.ComponentModel.DataAnnotations;

namespace Messenger_server.Models
{
    public class Message
    {
        [Key]
        public int Id { get; set; }

        public int ChatRoomId { get; set; }
        public ChatRoom ChatRoom { get; set; } = null!;

        public int SenderId { get; set; }
        public User Sender { get; set; } = null!;

        public string Content { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }

        public DateTime SentAt { get; set; } = DateTime.UtcNow;
    }
}