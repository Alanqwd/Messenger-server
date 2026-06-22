using Messenger_server.Data;
using Messenger_server.Filters;
using Messenger_server.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Messenger_server.Controllers
{
    public record CreateChatRequest(string AccessCode, string Name, int UserId, string? AvatarUrl, string? Description);
    public record JoinChatRequest(string AccessCode, int UserId);
    public record LeaveChatRequest(int ChatId, int UserId);
    public record MarkAsReadRequest(int ChatId, int UserId);
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
    [ServiceFilter(typeof(SessionAuthFilter))] 
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

            var token = Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();

            if (string.IsNullOrEmpty(token))
                return Unauthorized("No token provided");


            var user = await _context.Users.FindAsync(request.UserId);
            if (user == null || user.SessionToken != token)
                return Unauthorized("Session expired or logged in elsewhere");

            try
            {
                Console.WriteLine($"[CreateChat] Request: UserId={request.UserId}, Name={request.Name}, Code={request.AccessCode}");


                var userExists = await _context.Users.AnyAsync(u => u.Id == request.UserId);
                if (!userExists)
                {
                    Console.WriteLine($"[CreateChat] User {request.UserId} not found");
                    return BadRequest("User not found");
                }

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

                Console.WriteLine($"[CreateChat] Chat created with Id={chat.Id}");

                _context.UserChatRooms.Add(new UserChatRoom
                {
                    UserId = request.UserId,
                    ChatRoomId = chat.Id,
                    UnreadCount = 0
                });

                await _context.SaveChangesAsync();

                Console.WriteLine($"[CreateChat] UserChatRoom created successfully");

                return Ok(new ChatRoomDto
                {
                    Id = chat.Id,
                    Name = chat.Name,
                    AccessCode = chat.AccessCode,
                    AvatarUrl = chat.AvatarUrl,
                    Description = chat.Description
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CreateChat] ERROR: {ex.Message}");
                Console.WriteLine($"[CreateChat] StackTrace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"[CreateChat] Inner Exception: {ex.InnerException.Message}");
                }
                return StatusCode(500, $"Error creating chat: {ex.Message}");
            }
        }


        [HttpPost("mark-as-read")]
        public async Task<IActionResult> MarkAsRead([FromBody] MarkAsReadRequest request)
        {
            var userChat = await _context.UserChatRooms
                .FirstOrDefaultAsync(uc => uc.UserId == request.UserId && uc.ChatRoomId == request.ChatId);

            if (userChat != null)
            {
                userChat.UnreadCount = 0;
                await _context.SaveChangesAsync();
            }

            return Ok();
        }

        [HttpPost("join")]
        public async Task<IActionResult> JoinChat([FromBody] JoinChatRequest request)
        {
            try
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
            catch (Exception ex)
            {
                Console.WriteLine($"[JoinChat] ERROR: {ex.Message}");
                return StatusCode(500, $"Error joining chat: {ex.Message}");
            }
        }

        [HttpPost("leave")]
        public async Task<IActionResult> LeaveChat([FromBody] LeaveChatRequest request)
        {
            try
            {
                var userChat = await _context.UserChatRooms
                    .FirstOrDefaultAsync(uc => uc.UserId == request.UserId && uc.ChatRoomId == request.ChatId);

                if (userChat == null)
                    return NotFound("User is not in this chat");

                _context.UserChatRooms.Remove(userChat);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Successfully left the chat" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LeaveChat] ERROR: {ex.Message}");
                return StatusCode(500, $"Error leaving chat: {ex.Message}");
            }
        }

        [HttpGet("members/{chatId}")]
        public async Task<IActionResult> GetChatMembers(int chatId)
        {
            try
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
            catch (Exception ex)
            {
                Console.WriteLine($"[GetChatMembers] ERROR: {ex.Message}");
                return StatusCode(500, $"Error getting members: {ex.Message}");
            }
        }

        [HttpGet("my-chats/{userId}")]
        public async Task<IActionResult> GetMyChats(int userId)
        {
            try
            {
                Console.WriteLine($"[GetMyChats] Request for userId={userId}");

                var chats = await _context.UserChatRooms
                    .Where(uc => uc.UserId == userId)
                    .Select(uc => new ChatRoomDto
                    {
                        Id = uc.ChatRoom.Id,
                        Name = uc.ChatRoom.Name,
                        AccessCode = uc.ChatRoom.AccessCode,
                        UnreadCount = uc.UnreadCount,
                        AvatarUrl = uc.ChatRoom.AvatarUrl,
                        Description = uc.ChatRoom.Description
                    })
                    .ToListAsync();

                Console.WriteLine($"[GetMyChats] Found {chats.Count} chats");
                return Ok(chats);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetMyChats] ERROR: {ex.Message}");
                Console.WriteLine($"[GetMyChats] StackTrace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"[GetMyChats] Inner Exception: {ex.InnerException.Message}");
                }
                return StatusCode(500, $"Error getting chats: {ex.Message}");
            }
        }

        [HttpGet("messages/{chatId}")]
        public async Task<IActionResult> GetMessages(int chatId)
        {
            try
            {
                var messages = await _context.Messages
                    .Where(m => m.ChatRoomId == chatId)
                    .OrderBy(m => m.SentAt)
                    .Select(m => new MessageDto
                    {
                        Id = m.Id,
                        Content = m.Content,
                        ImageUrl = m.ImageUrl,
                        SentAt = m.SentAt,
                        SenderName = m.Sender.Username,
                        SenderAvatar = m.Sender.AvatarUrl,
                        SenderId = m.SenderId
                    })
                    .ToListAsync();

                return Ok(messages);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetMessages] ERROR: {ex.Message}");
                return StatusCode(500, $"Error getting messages: {ex.Message}");
            }
        }
    }
}