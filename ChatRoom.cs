using System.ComponentModel.DataAnnotations;

namespace Messenger_server.Models
{
    public class ChatRoom
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(10, MinimumLength = 10)]
        public string AccessCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        public int CreatedById { get; set; }
        public User CreatedBy { get; set; } = null!;

        public ICollection<Message> Messages { get; set; } = new List<Message>();
        public ICollection<UserChatRoom> UserChatRooms { get; set; } = new List<UserChatRoom>();
    }
}