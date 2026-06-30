using Messenger_server.Data;
using Messenger_server.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Messenger_server.Data;
using Messenger_server.Models;
using System.Security.Cryptography;
using System.Text;

namespace MoonMessenger.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AuthController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            Console.WriteLine($"[Register] Получен запрос: Username={request.Username}, Password length={request.Password?.Length}");

            try
            {
                if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                {
                    Console.WriteLine("[Register] Ошибка: Пустые поля");
                    return BadRequest("Username and Password are required");
                }

                if (await _context.Users.AnyAsync(u => u.Username == request.Username))
                {
                    Console.WriteLine($"[Register] Ошибка: Username '{request.Username}' уже существует");
                    return BadRequest("Username already exists");
                }

                var user = new User
                {
                    Username = request.Username,
                    PasswordHash = HashPassword(request.Password),
                    Bio = request.Bio,
                    AvatarUrl = request.AvatarUrl,
                    SessionToken = Guid.NewGuid().ToString()
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                Messenger_server.Hubs.ChatHub._onlineUsers.TryRemove(user.Id, out _);

                Console.WriteLine($"[Register] Успешно создан пользователь {user.Username} с ID {user.Id}");

                return Ok(new
                {
                    UserId = user.Id,
                    Username = user.Username,
                    AvatarUrl = user.AvatarUrl,  
                    Bio = user.Bio,              
                    SessionToken = user.SessionToken
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Register] КРИТИЧЕСКАЯ ОШИБКА: {ex.Message}");
                Console.WriteLine($"[Register] StackTrace: {ex.StackTrace}");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == request.Username);

                if (user == null)
                    return Unauthorized("Invalid credentials");

                var hash = HashPassword(request.Password);
                if (user.PasswordHash != hash)
                    return Unauthorized("Invalid credentials");

                if (Messenger_server.Hubs.ChatHub._onlineUsers.ContainsKey(user.Id))
                {
                    return BadRequest("Этот аккаунт уже используется другим пользователем");
                }

                var newToken = Guid.NewGuid().ToString();
                user.SessionToken = newToken;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    UserId = user.Id,
                    Username = user.Username,
                    AvatarUrl = user.AvatarUrl,
                    Bio = user.Bio,
                    SessionToken = newToken
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Login Error: {ex.Message}");
                return StatusCode(500, "Internal server error during login");
            }
        }

        protected bool IsSessionValid(int userId, string token)
        {
            var user = _context.Users.Find(userId);
            return user != null && user.SessionToken == token;
        }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
    }

    public record RegisterRequest(string Username, string Password, string? AvatarUrl, string? Bio);
    public record LoginRequest(string Username, string Password);
}