using Messenger_server.Data;
using Messenger_server.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Messenger_server.Data;
using Messenger_server.Models;

namespace Messenger_server.Hubs
{
    public class ChatHub : Hub
    {
        private readonly AppDbContext _context;

        public ChatHub(AppDbContext context)
        {
            _context = context;
        }

        public async Task JoinChat(int chatRoomId, int userId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"chat_{chatRoomId}");

            var userChat = await _context.UserChatRooms
                .FirstOrDefaultAsync(uc => uc.UserId == userId && uc.ChatRoomId == chatRoomId);

            if (userChat != null)
            {
                userChat.UnreadCount = 0;
                await _context.SaveChangesAsync();
                await Clients.Caller.SendAsync("UnreadCountsUpdated", userId);
            }
        }

        public async Task SendMessage(int chatRoomId, int senderId, string content, string? imageUrl)
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
                SenderName = sender?.Username,
                SenderAvatar = sender?.AvatarUrl,
                SenderId = senderId
            });

            await Clients.OthersInGroup($"chat_{chatRoomId}").SendAsync("UpdateUnreadBadge", chatRoomId);
        }
    }
}