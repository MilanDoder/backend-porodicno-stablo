using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PorodicnoStablo.Api.Data;
using PorodicnoStablo.Api.Dtos;
using PorodicnoStablo.Api.Entities;
using PorodicnoStablo.Api.Services;

namespace PorodicnoStablo.Api.Controllers;

[ApiController]
[Route("api/history")]
[Authorize]
public class HistoryController(AppDbContext db, SupabaseStorageService storage) : ControllerBase
{
    private const string PdfBucket = "history-pdfs";
    private const long MaxPdfBytes = 50 * 1024 * 1024;

    /// <summary>Priče BEZ base64 naslovnih slika — slika ide preko /cover endpointa.</summary>
    [HttpGet]
    public async Task<ActionResult<List<HistoryStoryDto>>> GetAll()
    {
        var stories = await db.HistoryStories.AsNoTracking()
            .OrderBy(s => s.StoryDate)
            .Select(s => new { s.Id, s.Title, s.Content, s.StoryDate, s.ImageType, HasCover = s.CoverImage != null, s.HavePdf, s.PdfPath, s.CreatedAt })
            .ToListAsync();

        return stories.Select(s => new HistoryStoryDto(
            s.Id, s.Title, s.Content, s.StoryDate, s.ImageType, s.HasCover,
            s.HavePdf, s.PdfPath, storage.GetPublicUrl(PdfBucket, s.PdfPath), s.CreatedAt
        )).ToList();
    }

    [HttpGet("{id:long}/cover")]
    public async Task<ActionResult> GetCover(long id)
    {
        var item = await db.HistoryStories.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new { s.CoverImage, s.ImageType })
            .FirstOrDefaultAsync();
        if (item?.CoverImage is null) return NotFound();

        Response.Headers.CacheControl = "private, max-age=86400";
        return File(Convert.FromBase64String(item.CoverImage), item.ImageType ?? "image/jpeg");
    }

    [HttpPost]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> Create([FromBody] SaveHistoryRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Title) || string.IsNullOrWhiteSpace(req.Content))
            return BadRequest(new { message = "Naslov i tekst su obavezni." });

        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        db.HistoryStories.Add(new HistoryStory
        {
            Title = req.Title.Trim(),
            Content = req.Content.Trim(),
            StoryDate = req.StoryDate,
            CoverImage = req.CoverImage,
            ImageType = req.ImageType ?? "image/jpeg",
            HavePdf = req.HavePdf,
            PdfPath = req.HavePdf ? req.PdfPath : null,
            CreatedBy = Guid.TryParse(sub, out var uid) ? uid : null,
        });
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> Update(long id, [FromBody] SaveHistoryRequest req)
    {
        var item = await db.HistoryStories.FirstOrDefaultAsync(s => s.Id == id);
        if (item is null) return NotFound();

        // Ako se PDF mijenja/uklanja — stari fajl briše SERVER iz Storage-a
        if (item.PdfPath is not null && (!req.HavePdf || (req.PdfPath is not null && req.PdfPath != item.PdfPath)))
            await storage.DeleteAsync(PdfBucket, item.PdfPath);

        item.Title = req.Title.Trim();
        item.Content = req.Content.Trim();
        item.StoryDate = req.StoryDate;
        item.HavePdf = req.HavePdf;
        item.PdfPath = req.HavePdf ? (req.PdfPath ?? item.PdfPath) : null;

        if (req.RemoveCover) { item.CoverImage = null; }
        else if (!string.IsNullOrEmpty(req.CoverImage))
        {
            item.CoverImage = req.CoverImage;
            item.ImageType = req.ImageType ?? "image/jpeg";
        }

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> Delete(long id)
    {
        var item = await db.HistoryStories.FirstOrDefaultAsync(s => s.Id == id);
        if (item is null) return NotFound();

        if (item.PdfPath is not null)
            await storage.DeleteAsync(PdfBucket, item.PdfPath); // fajl + red zajedno, ne curi storage

        db.HistoryStories.Remove(item);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Upload PDF-a — ide kroz backend sa service_role ključem,
    /// tako da anon klijenti više ne moraju imati write pravo na bucket.
    /// </summary>
    [HttpPost("pdf")]
    [RequestSizeLimit(MaxPdfBytes)]
    public async Task<ActionResult> UploadPdf(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new { message = "Fajl je obavezan." });
        if (file.Length > MaxPdfBytes) return BadRequest(new { message = "PDF mora biti manji od 50MB." });
        if (!string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Dozvoljen je samo PDF." });

        var clean = Path.GetFileNameWithoutExtension(file.FileName);
        clean = new string(clean.Where(c => char.IsLetterOrDigit(c) || c is ' ' or '_' or '-').ToArray())
            .Trim().Replace(' ', '_');
        if (clean.Length > 40) clean = clean[..40];
        var path = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{clean}.pdf";

        await using var stream = file.OpenReadStream();
        await storage.UploadAsync(PdfBucket, path, stream, "application/pdf", ct);

        return Ok(new { path, url = storage.GetPublicUrl(PdfBucket, path) });
    }

    /// <summary>Brisanje PDF-a iz Storage-a (npr. korisnik uklonio fajl u formi prije slanja).</summary>
    [HttpDelete("pdf")]
    public async Task<ActionResult> DeletePdf([FromQuery] string path, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains("..") || path.Contains('/'))
            return BadRequest(new { message = "Neispravna putanja." });
        await storage.DeleteAsync(PdfBucket, path, ct);
        return NoContent();
    }
}
