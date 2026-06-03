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
                    AvatarUrl = request.AvatarUrl
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                return Ok(new { UserId = user.Id, Username = user.Username });
            }
            catch (Exception ex)
            {
                // Это запишет ошибку в консоль Visual Studio, чтобы вы видели причину
                Console.WriteLine($"Registration Error: {ex.Message}");
                Console.WriteLine($"Inner Exception: {ex.InnerException?.Message}");
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

                // Сравниваем хеши
                var hash = HashPassword(request.Password);
                if (user.PasswordHash != hash)
                    return Unauthorized("Invalid credentials");

                return Ok(new { UserId = user.Id, Username = user.Username, AvatarUrl = user.AvatarUrl, Bio = user.Bio });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Login Error: {ex.Message}");
                return StatusCode(500, "Internal server error during login");
            }
        }

        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            try
            {
                var user = await _context.Users.FindAsync(request.UserId);
                if (user == null) return NotFound();

                if (request.Username != user.Username)
                {
                    if (await _context.Users.AnyAsync(u => u.Username == request.Username))
                        return BadRequest("Username already taken");
                    user.Username = request.Username;
                }

                user.Bio = request.Bio;
                user.AvatarUrl = request.AvatarUrl;

                await _context.SaveChangesAsync();
                return Ok(user);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Profile Update Error: {ex.Message}");
                return StatusCode(500, "Internal server error during profile update");
            }
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
    public record UpdateProfileRequest(int UserId, string Username, string? AvatarUrl, string? Bio);
}