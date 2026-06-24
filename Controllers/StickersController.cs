
using Messenger_server.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
public class StickersController : ControllerBase
{
    private readonly AppDbContext _context;

    public StickersController(AppDbContext context)
    {
        _context = context;
    }

 
    [HttpGet("packs")]
    public async Task<IActionResult> GetPacks()
    {
        var packs = await _context.StickerPacks
            .Include(p => p.Stickers)
            .ToListAsync();
        return Ok(packs);
    }


    [HttpPost("packs")]
    public async Task<IActionResult> CreatePack([FromBody] CreatePackDto dto)
    {
        var pack = new StickerPack
        {
            Name = dto.Name,
            CoverUrl = dto.CoverUrl
        };
        _context.StickerPacks.Add(pack);
        await _context.SaveChangesAsync();
        return Ok(pack);
    }

    [HttpPost("stickers")]
    public async Task<IActionResult> AddSticker([FromBody] AddStickerDto dto)
    {
        var sticker = new Sticker
        {
            StickerPackId = dto.PackId,
            ImageUrl = dto.ImageUrl,
            Emoji = dto.Emoji
        };
        _context.Stickers.Add(sticker);
        await _context.SaveChangesAsync();
        return Ok(sticker);
    }
}

public record CreatePackDto(string Name, string CoverUrl);
public record AddStickerDto(int PackId, string ImageUrl, string? Emoji);