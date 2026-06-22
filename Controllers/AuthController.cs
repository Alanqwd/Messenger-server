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
            try
            {
                if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                    return BadRequest("Username and Password are required");

                if (await _context.Users.AnyAsync(u => u.Username == request.Username))
                    return BadRequest("Username already exists");

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

                return Ok(new { UserId = user.Id, Username = user.Username, SessionToken = user.SessionToken });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Registration Error: {ex.Message}");
                return StatusCode(500, "Internal server error during registration");
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