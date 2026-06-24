
public class StickerPack
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CoverUrl { get; set; } = string.Empty; 
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Sticker> Stickers { get; set; } = new List<Sticker>();
}


public class Sticker
{
    public int Id { get; set; }
    public int StickerPackId { get; set; }
    public string ImageUrl { get; set; } = string.Empty; 
    public string? Emoji { get; set; }

    public StickerPack StickerPack { get; set; } = null!;
}