using Messenger_server.Data;
using Messenger_server.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace Messenger_server.Hubs
{
    public class ChatHub : Hub
    {
        private readonly AppDbContext _context;
        public static readonly ConcurrentDictionary<int, string> _onlineUsers = new();
        private static readonly ConcurrentDictionary<string, int> _connectionToUser = new();
        public class UserStatusResponse
        {
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
            public List<int> ChatIds { get; set; } = new();
        }

        public class JoinChatResponse
        {
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
            public List<MessageDto> Messages { get; set; } = new();
        }

        public class SendMessageResponse
        {
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
            public int? MessageId { get; set; }
        }

        public class MessageDto
        {
            public int Id { get; set; }
            public int ChatRoomId { get; set; }
            public int SenderId { get; set; }
            public string SenderName { get; set; } = string.Empty;
            public string? SenderAvatar { get; set; }
            public string Content { get; set; } = string.Empty;
            public string? ImageUrl { get; set; }
            public string? StickerUrl { get; set; }
            public DateTime SentAt { get; set; }
        }
        public ChatHub(AppDbContext context)
        {
            _context = context;
        }

        public override async Task OnConnectedAsync()
        {
            Console.WriteLine($"Новое подключение: {Context.ConnectionId}");
            await base.OnConnectedAsync();
        }

        public async Task<UserStatusResponse> UserConnected(int userId, string token)
        {
            try
            {
                Console.WriteLine($"[UserConnected] Попытка подключения пользователя {userId}");

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (user == null)
                {
                    Console.WriteLine($"[UserConnected] Пользователь {userId} не найден");
                    Context.Abort();
                    return new UserStatusResponse { Success = false, Message = "User not found" };
                }

                if (user.SessionToken != token)
                {
                    Console.WriteLine($"[UserConnected] Неверный токен для пользователя {userId}");
                    Context.Abort();
                    return new UserStatusResponse { Success = false, Message = "Invalid token" };
                }

                if (_onlineUsers.TryGetValue(userId, out var oldConnectionId))
                {
                    _onlineUsers.TryRemove(userId, out _);
                    _connectionToUser.TryRemove(oldConnectionId, out _);
                    Console.WriteLine($"[UserConnected] Удалено старое подключение для пользователя {userId}");
                }

                _onlineUsers[userId] = Context.ConnectionId;
                _connectionToUser[Context.ConnectionId] = userId;

                Console.WriteLine($"[UserConnected] Пользователь {userId} успешно подключен");

                var userChats = await _context.UserChatRooms
                    .Where(uc => uc.UserId == userId)
                    .Select(uc => uc.ChatRoomId)
                    .ToListAsync();

                foreach (var chatId in userChats)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"chat_{chatId}");
                    Console.WriteLine($"[UserConnected] Автоподписка на чат {chatId}");
                }

                await NotifyUserStatusChange(userId, true);

                return new UserStatusResponse
                {
                    Success = true,
                    Message = "Connected",
                    ChatIds = userChats
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UserConnected] КРИТИЧЕСКАЯ ОШИБКА: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Context.Abort();
                return new UserStatusResponse { Success = false, Message = ex.Message };
            }
        }

        public async Task<JoinChatResponse> JoinChat(int chatRoomId, int userId)
        {
            try
            {
                Console.WriteLine($"[JoinChat] Пользователь {userId} пытается войти в чат {chatRoomId}");

                var userChat = await _context.UserChatRooms
                    .FirstOrDefaultAsync(uc => uc.UserId == userId && uc.ChatRoomId == chatRoomId);

                if (userChat == null)
                {
                    Console.WriteLine($"[JoinChat] Пользователь {userId} не является участником чата {chatRoomId}");
                    return new JoinChatResponse { Success = false, Message = "Not a member" };
                }

                await Groups.AddToGroupAsync(Context.ConnectionId, $"chat_{chatRoomId}");
                Console.WriteLine($"[JoinChat] Пользователь {userId} добавлен в группу chat_{chatRoomId}");

                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                {
                    await Clients.OthersInGroup($"chat_{chatRoomId}").SendAsync(
                        "UserJoinedChat",
                        userId,
                        user.Username,
                        user.AvatarUrl
                    );
                }

                var recentMessages = await _context.Messages
                    .Where(m => m.ChatRoomId == chatRoomId)
                    .OrderByDescending(m => m.SentAt)
                    .Take(50)
                    .OrderBy(m => m.SentAt)
                    .Select(m => new MessageDto
                    {
                        Id = m.Id,
                        ChatRoomId = m.ChatRoomId,
                        SenderId = m.SenderId,
                        SenderName = m.Sender.Username,
                        SenderAvatar = m.Sender.AvatarUrl,
                        Content = m.Content,
                        ImageUrl = m.ImageUrl,
                        StickerUrl = m.StickerUrl,
                        SentAt = m.SentAt
                    })
                    .ToListAsync();

                Console.WriteLine($"[JoinChat] Отправлено {recentMessages.Count} сообщений пользователю {userId}");

                return new JoinChatResponse
                {
                    Success = true,
                    Message = "Joined",
                    Messages = recentMessages
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[JoinChat] ОШИБКА: {ex.Message}");
                return new JoinChatResponse { Success = false, Message = ex.Message };
            }
        }

     
        public async Task<SendMessageResponse> SendMessage(int chatRoomId, int userId, string content, string imageUrl, string stickerUrl)
        {
            try
            {
                Console.WriteLine($"[SendMessage] Получен запрос: ChatId={chatRoomId}, UserId={userId}, Content='{content}', ImageUrl='{imageUrl}', StickerUrl='{stickerUrl}'");

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    Console.WriteLine($"[SendMessage] Пользователь {userId} не найден");
                    return new SendMessageResponse { Success = false, Message = "User not found" };
                }

                
                var finalImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl;
                var finalStickerUrl = string.IsNullOrWhiteSpace(stickerUrl) ? null : stickerUrl;
                var finalContent = string.IsNullOrWhiteSpace(content) ? string.Empty : content;

                var message = new Message
                {
                    ChatRoomId = chatRoomId,
                    SenderId = userId,
                    Content = finalContent,
                    ImageUrl = finalImageUrl,
                    StickerUrl = finalStickerUrl,
                    SentAt = DateTime.UtcNow
                };

                _context.Messages.Add(message);
                await _context.SaveChangesAsync();

                Console.WriteLine($"[SendMessage] Сообщение сохранено с ID {message.Id}");

                var messageDto = new MessageDto
                {
                    Id = message.Id,
                    ChatRoomId = message.ChatRoomId,
                    SenderId = userId,
                    SenderName = user.Username,
                    SenderAvatar = user.AvatarUrl,
                    Content = message.Content,
                    ImageUrl = message.ImageUrl,
                    StickerUrl = message.StickerUrl,
                    SentAt = message.SentAt
                };

                var groupName = $"chat_{chatRoomId}";
                Console.WriteLine($"[SendMessage] Отправка в группу {groupName}");
                await Clients.Group(groupName).SendAsync("ReceiveMessage", messageDto);

                return new SendMessageResponse
                {
                    Success = true,
                    Message = "Sent",
                    MessageId = message.Id
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SendMessage] 🔥 ОШИБКА: {ex.Message}");
                Console.WriteLine($"[SendMessage] StackTrace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"[SendMessage] Inner: {ex.InnerException.Message}");
                }
                return new SendMessageResponse { Success = false, Message = ex.Message };
            }
        }

        public async Task LeaveChatFromHub(int chatRoomId, int userId)
        {
            try
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"chat_{chatRoomId}");

                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                {
                    await Clients.OthersInGroup($"chat_{chatRoomId}").SendAsync(
                        "UserLeftChat",
                        userId,
                        user.Username,
                        user.AvatarUrl
                    );
                }

                Console.WriteLine($"[LeaveChat] Пользователь {userId} покинул чат {chatRoomId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LeaveChat] ОШИБКА: {ex.Message}");
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (_connectionToUser.TryRemove(Context.ConnectionId, out var userId))
            {
                _onlineUsers.TryRemove(userId, out _);
                Console.WriteLine($"[OnDisconnected] Пользователь {userId} отключился");

                await NotifyUserStatusChange(userId, false);
            }

            await base.OnDisconnectedAsync(exception);
        }

        private async Task NotifyUserStatusChange(int userId, bool isOnline)
        {
            try
            {
                var userChats = await _context.UserChatRooms
                    .Where(uc => uc.UserId == userId)
                    .Select(uc => uc.ChatRoomId)
                    .ToListAsync();

                foreach (var chatId in userChats)
                {
                    await Clients.Group($"chat_{chatId}").SendAsync("UserStatusChanged", userId, isOnline);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NotifyUserStatusChange] Ошибка: {ex.Message}");
            }
        }

        public async Task<List<int>> GetOnlineUsersInChat(int chatRoomId)
        {
            var members = await _context.UserChatRooms
                .Where(uc => uc.ChatRoomId == chatRoomId)
                .Select(uc => uc.UserId)
                .ToListAsync();

            return members.Where(id => _onlineUsers.ContainsKey(id)).ToList();
        }
    }

}