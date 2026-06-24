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

        public ChatHub(AppDbContext context)
        {
            _context = context;
        }

        public async Task UserConnected(int userId, string token)
        {
            try
            {
                Console.WriteLine($"Attempting connection for User {userId}");


                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                {
                    Console.WriteLine($"User {userId} not found in DB.");
                    Context.Abort();
                    return;
                }

                if (user.SessionToken != token)
                {
                    Console.WriteLine($"Invalid session token for User {userId}. Expected: {user.SessionToken}, Got: {token}");
    
                    Context.Abort();
                    return;
                }

             
                if (_onlineUsers.ContainsKey(userId))
                {
                    _onlineUsers.TryRemove(userId, out _);
                }

                _onlineUsers[userId] = Context.ConnectionId;
                Console.WriteLine($"User {userId} successfully connected.");


                await NotifyUserStatusChange(userId, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CRITICAL ERROR in UserConnected: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Context.Abort();
            }
        }

        public async Task JoinChat(int chatRoomId, int userId)
        {
            try
            {
                var userChat = await _context.UserChatRooms
                    .FirstOrDefaultAsync(uc => uc.UserId == userId && uc.ChatRoomId == chatRoomId);

                if (userChat == null)
                {
                    Console.WriteLine($"User {userId} tried to join chat {chatRoomId} but is not a member.");
                    return;
                }

                await Groups.AddToGroupAsync(Context.ConnectionId, $"chat_{chatRoomId}");

                if (userChat.UnreadCount > 0)
                {
                    userChat.UnreadCount = 0;
                    await _context.SaveChangesAsync();
                    await Clients.Caller.SendAsync("UnreadCountsUpdated");
                }

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

                Console.WriteLine($"User {userId} joined group chat_{chatRoomId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in JoinChat: {ex.Message}");
            }
        }
        public async Task SendSticker(int chatRoomId, int userId, string stickerUrl)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return;

            var message = new Message
            {
                ChatRoomId = chatRoomId,
                SenderId = userId,
                StickerUrl = stickerUrl, 
                SentAt = DateTime.UtcNow
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            var groupName = $"Chat_{chatRoomId}";
            await Clients.Group(groupName).SendAsync("ReceiveSticker", new
            {
                message.Id,
                message.ChatRoomId,
                SenderId = userId,
                SenderName = user.Username,
                SenderAvatar = user.AvatarUrl,
                message.StickerUrl,
                message.SentAt
            });
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

                Console.WriteLine($"User {userId} left group chat_{chatRoomId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in LeaveChatFromHub: {ex.Message}");
            }
        }

        public async Task SendMessage(int chatRoomId, int userId, string content, string? imageUrl)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return;

            var message = new Message
            {
                ChatRoomId = chatRoomId,
                SenderId = userId,
                Content = content,
                ImageUrl = imageUrl,   // ✅ Сохраняем URL стикера
                SentAt = DateTime.UtcNow
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            var groupName = $"Chat_{chatRoomId}";
            await Clients.Group(groupName).SendAsync("ReceiveMessage", new
            {
                message.Id,
                message.ChatRoomId,
                SenderId = userId,
                SenderName = user.Username,
                SenderAvatar = user.AvatarUrl,
                message.Content,
                message.ImageUrl,      
                message.SentAt
            });
        }
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
  
            var userId = _onlineUsers.FirstOrDefault(x => x.Value == Context.ConnectionId).Key;

            if (userId != 0)
            {
                _onlineUsers.TryRemove(userId, out _);
                Console.WriteLine($"User {userId} disconnected.");
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
                Console.WriteLine($"Error notifying status change: {ex.Message}");
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