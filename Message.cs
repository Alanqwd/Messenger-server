using System.ComponentModel.DataAnnotations;

namespace Messenger_server.Models
{
    public class Message
    {
        public int Id { get; set; }
        public int ChatRoomId { get; set; }
        public int SenderId { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public string? StickerUrl { get; set; } 
        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        public ChatRoom ChatRoom { get; set; } = null!;
        public User Sender { get; set; } = null!;
    }
}