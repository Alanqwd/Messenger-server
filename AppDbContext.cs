using Messenger_server.Models;
using Microsoft.EntityFrameworkCore;

namespace Messenger_server.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        public DbSet<StickerPack> StickerPacks { get; set; }
        public DbSet<Sticker> Stickers { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<ChatRoom> ChatRooms { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<UserChatRoom> UserChatRooms { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            
            modelBuilder.Entity<UserChatRoom>()
                .HasKey(uc => new { uc.UserId, uc.ChatRoomId });

      
            modelBuilder.Entity<UserChatRoom>()
                .HasOne(uc => uc.User)
                .WithMany()
                .HasForeignKey(uc => uc.UserId)
                .OnDelete(DeleteBehavior.Cascade);

          
            modelBuilder.Entity<UserChatRoom>()
                .HasOne(uc => uc.ChatRoom)
                .WithMany(c => c.UserChatRooms)
                .HasForeignKey(uc => uc.ChatRoomId)
                .OnDelete(DeleteBehavior.Cascade);

         
            modelBuilder.Entity<ChatRoom>()
                .HasIndex(c => c.AccessCode)
                .IsUnique();

     
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

  
            modelBuilder.Entity<ChatRoom>()
                .HasOne(c => c.CreatedBy)
                .WithMany()
                .HasForeignKey(c => c.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

      
            modelBuilder.Entity<Message>()
                .HasOne(m => m.ChatRoom)
                .WithMany(c => c.Messages)
                .HasForeignKey(m => m.ChatRoomId)
                .OnDelete(DeleteBehavior.Cascade);

            
            modelBuilder.Entity<Message>()
                .HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}