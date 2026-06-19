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

       
        private static readonly ConcurrentDictionary<int, string> _onlineUsers = new();

        public ChatHub(AppDbContext context)
        {
            _context = context;
        }

        public async Task JoinChat(int chatRoomId, int userId)
        {
            try
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"chat_{chatRoomId}");

                var userChat = await _context.UserChatRooms
                    .FirstOrDefaultAsync(uc => uc.UserId == userId && uc.ChatRoomId == chatRoomId);

                if (userChat != null)
                {
                    userChat.UnreadCount = 0;
                    await _context.SaveChangesAsync();
                    await Clients.Caller.SendAsync("UnreadCountsUpdated");
                }

                Console.WriteLine($"User {userId} joined chat {chatRoomId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in JoinChat: {ex.Message}");
                throw;
            }
        }

        public async Task SendMessage(int chatRoomId, int senderId, string content, string? imageUrl)
        {
            try
            {
                var message = new Message
                {
                    ChatRoomId = chatRoomId,
                    SenderId = senderId,
                    Content = content,
                    ImageUrl = imageUrl,
                    SentAt = DateTime.UtcNow
                };

                _context.Messages.Add(message);
                await _context.SaveChangesAsync();

                var participants = await _context.UserChatRooms
                    .Where(uc => uc.ChatRoomId == chatRoomId && uc.UserId != senderId)
                    .ToListAsync();

                foreach (var participant in participants)
                {
                    participant.UnreadCount++;
                }

                await _context.SaveChangesAsync();

                var sender = await _context.Users.FindAsync(senderId);

                await Clients.Group($"chat_{chatRoomId}").SendAsync("ReceiveMessage", new
                {
                    message.Id,
                    message.Content,
                    message.ImageUrl,
                    message.SentAt,
                    SenderName = sender?.Username ?? "Unknown",
                    SenderAvatar = sender?.AvatarUrl,
                    SenderId = senderId,
                    ChatRoomId = chatRoomId
                });

                await Clients.OthersInGroup($"chat_{chatRoomId}").SendAsync("UpdateUnreadBadge", chatRoomId);

                Console.WriteLine($"Message sent in chat {chatRoomId} by user {senderId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in SendMessage: {ex.Message}");
                throw;
            }
        }

        public async Task UserConnected(int userId)
        {
            _onlineUsers[userId] = Context.ConnectionId;
            Console.WriteLine($"User {userId} connected with connection {Context.ConnectionId}");

            await NotifyUserStatusChange(userId, true);
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
         
            var userId = _onlineUsers.FirstOrDefault(x => x.Value == Context.ConnectionId).Key;

            if (userId != 0)
            {
                _onlineUsers.TryRemove(userId, out _);
                Console.WriteLine($"User {userId} disconnected");

             
                await NotifyUserStatusChange(userId, false);
            }

            Console.WriteLine($"Client disconnected: {Context.ConnectionId}");
            await base.OnDisconnectedAsync(exception);
        }

    
        private async Task NotifyUserStatusChange(int userId, bool isOnline)
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