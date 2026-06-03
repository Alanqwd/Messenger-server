using Messenger_server.Data;
using Messenger_server.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Messenger_server.Data;
using Messenger_server.Models;

namespace Messenger_server.Controllers
{
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
            if (await _context.ChatRooms.AnyAsync(c => c.AccessCode == request.AccessCode))
                return BadRequest("Access code already exists");

            var chat = new ChatRoom
            {
                AccessCode = request.AccessCode,
                Name = request.Name,
                CreatedById = request.UserId
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
            return Ok(chat);
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

            return Ok(chat);
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
                    uc.UnreadCount
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

    public record CreateChatRequest(string AccessCode, string Name, int UserId);
    public record JoinChatRequest(string AccessCode, int UserId);
}