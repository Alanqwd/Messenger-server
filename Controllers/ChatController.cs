using Messenger_server.Data;
using Messenger_server.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Messenger_server.Controllers
{
    public record CreateChatRequest(string AccessCode, string Name, int UserId, string? AvatarUrl, string? Description);
    public record JoinChatRequest(string AccessCode, int UserId);
    public record LeaveChatRequest(int ChatId, int UserId);

    public class ChatRoomDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string AccessCode { get; set; } = string.Empty;
        public int UnreadCount { get; set; }
        public string? AvatarUrl { get; set; }
        public string? Description { get; set; } 
    }

    public class MessageDto
    {
        public int Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public DateTime SentAt { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string? SenderAvatar { get; set; }
        public int SenderId { get; set; }
    }

    public class ChatMemberDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public bool IsOnline { get; set; }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ChatController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateChat([FromBody] CreateChatRequest request)
        {
            var code = request.AccessCode?.Trim();

            if (string.IsNullOrEmpty(code) || code.Length != 10)
                return BadRequest("Access code must be exactly 10 characters");

            if (await _context.ChatRooms.AnyAsync(c => c.AccessCode == code))
                return BadRequest("Access code already exists");

            var chat = new ChatRoom
            {
                AccessCode = code,
                Name = request.Name,
                CreatedById = request.UserId,
                AvatarUrl = request.AvatarUrl, 
                Description = request.Description 
            };

            _context.ChatRooms.Add(chat);
            await _context.SaveChangesAsync();

            _context.UserChatRooms.Add(new UserChatRoom
            {
                UserId = request.UserId,
                ChatRoomId = chat.Id,
                UnreadCount = 0
            });

            await _context.SaveChangesAsync();

            return Ok(new ChatRoomDto
            {
                Id = chat.Id,
                Name = chat.Name,
                AccessCode = chat.AccessCode,
                AvatarUrl = chat.AvatarUrl,
                Description = chat.Description
            });
        }

        [HttpPost("join")]
        public async Task<IActionResult> JoinChat([FromBody] JoinChatRequest request)
        {
            var chat = await _context.ChatRooms.FirstOrDefaultAsync(c => c.AccessCode == request.AccessCode);
            if (chat == null) return NotFound("Chat not found");

            var existingLink = await _context.UserChatRooms
                .AnyAsync(uc => uc.UserId == request.UserId && uc.ChatRoomId == chat.Id);

            if (!existingLink)
            {
                _context.UserChatRooms.Add(new UserChatRoom
                {
                    UserId = request.UserId,
                    ChatRoomId = chat.Id,
                    UnreadCount = 0
                });
                await _context.SaveChangesAsync();
            }

            return Ok(new ChatRoomDto
            {
                Id = chat.Id,
                Name = chat.Name,
                AccessCode = chat.AccessCode,
                UnreadCount = 0,
                AvatarUrl = chat.AvatarUrl,
                Description = chat.Description
            });
        }

        [HttpPost("leave")]
        public async Task<IActionResult> LeaveChat([FromBody] LeaveChatRequest request)
        {
            var userChat = await _context.UserChatRooms
                .FirstOrDefaultAsync(uc => uc.UserId == request.UserId && uc.ChatRoomId == request.ChatId);

            if (userChat == null)
                return NotFound("User is not in this chat");

            _context.UserChatRooms.Remove(userChat);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Successfully left the chat" });
        }


        [HttpGet("members/{chatId}")]
        public async Task<IActionResult> GetChatMembers(int chatId)
        {
            var members = await _context.UserChatRooms
                .Where(uc => uc.ChatRoomId == chatId)
                .Select(uc => new ChatMemberDto
                {
                    UserId = uc.UserId,
                    Username = uc.User.Username,
                    AvatarUrl = uc.User.AvatarUrl,
                    IsOnline = false 
                })
                .ToListAsync();

            return Ok(members);
        }

        [HttpGet("my-chats/{userId}")]
        public async Task<IActionResult> GetMyChats(int userId)
        {
            var chats = await _context.UserChatRooms
                .Where(uc => uc.UserId == userId)
                .Select(uc => new
                {
                    uc.ChatRoom.Id,
                    uc.ChatRoom.Name,
                    uc.ChatRoom.AccessCode,
                    uc.UnreadCount,
                    uc.ChatRoom.AvatarUrl,
                    uc.ChatRoom.Description 
                })
                .ToListAsync();

            return Ok(chats);
        }

        [HttpGet("messages/{chatId}")]
        public async Task<IActionResult> GetMessages(int chatId)
        {
            var messages = await _context.Messages
                .Where(m => m.ChatRoomId == chatId)
                .OrderBy(m => m.SentAt)
                .Select(m => new
                {
                    m.Id,
                    m.Content,
                    m.ImageUrl,
                    m.SentAt,
                    SenderName = m.Sender.Username,
                    SenderAvatar = m.Sender.AvatarUrl,
                    m.SenderId
                })
                .ToListAsync();

            return Ok(messages);
        }
    }
}