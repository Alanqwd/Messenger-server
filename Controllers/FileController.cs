using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace Messenger_server.Controllers
{
        [ApiController]
        [Route("api/[controller]")]
        public class FileController : ControllerBase
        {
            private readonly IWebHostEnvironment _environment;

            public FileController(IWebHostEnvironment environment)
            {
                _environment = environment;
            }

            [HttpPost("upload")]
            [RequestSizeLimit(10 * 1024 * 1024)] // 10MB лимит
            public async Task<IActionResult> Upload(IFormFile file)
            {
                if (file == null || file.Length == 0)
                    return BadRequest("Файл не выбран");

       
                var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };
                var provider = new FileExtensionContentTypeProvider();

                if (!provider.TryGetContentType(file.FileName, out var contentType) ||
                    !allowedTypes.Contains(contentType.ToLower()))
                {
                    return BadRequest("Разрешены только изображения (JPG, PNG, GIF, WEBP)");
                }

           
                if (file.Length > 10 * 1024 * 1024)
                    return BadRequest("Файл слишком большой (максимум 10MB)");

                try
                {
                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads");

                 
                    if (!Directory.Exists(uploadsFolder))
                        Directory.CreateDirectory(uploadsFolder);

                    
                    var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
                    var filePath = Path.Combine(uploadsFolder, fileName);

                    
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    
                    var url = $"{Request.Scheme}://{Request.Host}/uploads/{fileName}";

                    return Ok(new { url, fileName });
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Ошибка при загрузке файла: {ex.Message}");
                }
            }

            [HttpDelete("{fileName}")]
            public IActionResult Delete(string fileName)
            {
                try
                {
                    var filePath = Path.Combine(_environment.WebRootPath, "uploads", fileName);

                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                        return Ok();
                    }

                    return NotFound("Файл не найден");
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Ошибка при удалении: {ex.Message}");
                }
            }
        }
    }
