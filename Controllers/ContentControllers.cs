using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PorodicnoStablo.Api.Data;
using PorodicnoStablo.Api.Dtos;
using PorodicnoStablo.Api.Entities;

namespace PorodicnoStablo.Api.Controllers;

// ─────────────────────────────────────────────────────────────────────────────
// OBAVJEŠTENJA
// ─────────────────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/announcements")]
[Authorize]
public class AnnouncementsController(AppDbContext db) : ControllerBase
{
    /// <summary>Aktivna obavještenja za banner (expires_at >= danas).</summary>
    [HttpGet("active")]
    public async Task<ActionResult<List<Announcement>>> GetActive()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await db.Announcements.AsNoTracking()
            .Where(a => a.ExpiresAt >= today && a.CreatedAt <= DateTime.UtcNow)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    [HttpGet]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<List<Announcement>>> GetAll()
        => await db.Announcements.AsNoTracking().OrderByDescending(a => a.ExpiresAt).ToListAsync();

    [HttpPost]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> Create([FromBody] SaveAnnouncementRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Message)) return BadRequest(new { message = "Tekst je obavezan." });
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        db.Announcements.Add(new Announcement
        {
            Message = req.Message.Trim(),
            ExpiresAt = req.ExpiresAt,
            CreatedBy = Guid.TryParse(sub, out var uid) ? uid : null,
        });
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> Update(long id, [FromBody] SaveAnnouncementRequest req)
    {
        var ann = await db.Announcements.FirstOrDefaultAsync(a => a.Id == id);
        if (ann is null) return NotFound();
        ann.Message = req.Message.Trim();
        ann.ExpiresAt = req.ExpiresAt;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> Delete(long id)
    {
        var ann = await db.Announcements.FirstOrDefaultAsync(a => a.Id == id);
        if (ann is null) return NotFound();
        db.Announcements.Remove(ann);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// GALERIJA — ključna optimizacija: lista NE vraća base64, slika ide kao binarni
// stream sa keš headerima. Šema baze ostaje ista (bez migracije podataka).
// ─────────────────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/gallery")]
[Authorize]
public class GalleryController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<GalleryItemDto>>> GetAll()
        => await db.Gallery.AsNoTracking()
            .OrderByDescending(g => g.PhotoYear)
            .Select(g => new GalleryItemDto(g.Id, g.Title, g.Description, g.PhotoYear, g.ImageType, g.ImageData != null))
            .ToListAsync();

    /// <summary>Binarna slika (dekodovan base64 iz baze) + keš headeri.</summary>
    [HttpGet("{id:long}/image")]
    public async Task<ActionResult> GetImage(long id)
    {
        var item = await db.Gallery.AsNoTracking()
            .Where(g => g.Id == id)
            .Select(g => new { g.ImageData, g.ImageType })
            .FirstOrDefaultAsync();
        if (item?.ImageData is null) return NotFound();

        Response.Headers.CacheControl = "private, max-age=86400";
        return File(Convert.FromBase64String(item.ImageData), item.ImageType ?? "image/jpeg");
    }

    [HttpPost]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> Create([FromBody] SaveGalleryRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Title) || string.IsNullOrEmpty(req.ImageData))
            return BadRequest(new { message = "Naslov i slika su obavezni." });

        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        db.Gallery.Add(new GalleryItem
        {
            Title = req.Title.Trim(),
            Description = req.Description,
            ImageData = req.ImageData,
            ImageType = req.ImageType ?? "image/jpeg",
            PhotoYear = req.PhotoYear,
            CreatedBy = Guid.TryParse(sub, out var uid) ? uid : null,
        });
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> Update(long id, [FromBody] SaveGalleryRequest req)
    {
        var item = await db.Gallery.FirstOrDefaultAsync(g => g.Id == id);
        if (item is null) return NotFound();

        item.Title = req.Title.Trim();
        item.Description = req.Description;
        item.PhotoYear = req.PhotoYear;
        if (!string.IsNullOrEmpty(req.ImageData)) // null = zadrži postojeću sliku
        {
            item.ImageData = req.ImageData;
            item.ImageType = req.ImageType ?? "image/jpeg";
        }
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> Delete(long id)
    {
        var item = await db.Gallery.FirstOrDefaultAsync(g => g.Id == id);
        if (item is null) return NotFound();
        db.Gallery.Remove(item);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// SEOBE — ista optimizacija kao galerija
// ─────────────────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/seobe")]
[Authorize(Policy = "Admin")] // trenutni UI prikazuje Seobe samo adminu
public class SeobeController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<SeobaDto>>> GetAll()
        => await db.Seobe.AsNoTracking()
            .OrderBy(s => s.Year)
            .Select(s => new SeobaDto(s.Id, s.Title, s.Description, s.Year, s.ImageType, s.ImageData != null))
            .ToListAsync();

    [HttpGet("{id:long}/image")]
    public async Task<ActionResult> GetImage(long id)
    {
        var item = await db.Seobe.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new { s.ImageData, s.ImageType })
            .FirstOrDefaultAsync();
        if (item?.ImageData is null) return NotFound();

        Response.Headers.CacheControl = "private, max-age=86400";
        return File(Convert.FromBase64String(item.ImageData), item.ImageType ?? "image/jpeg");
    }

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] SaveSeobaRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Title) || req.Year == 0 || string.IsNullOrEmpty(req.ImageData))
            return BadRequest(new { message = "Naslov, godina i slika su obavezni." });

        db.Seobe.Add(new Seoba
        {
            Title = req.Title.Trim(),
            Description = req.Description,
            Year = req.Year,
            ImageData = req.ImageData,
            ImageType = req.ImageType ?? "image/jpeg",
        });
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult> Update(long id, [FromBody] SaveSeobaRequest req)
    {
        var item = await db.Seobe.FirstOrDefaultAsync(s => s.Id == id);
        if (item is null) return NotFound();

        item.Title = req.Title.Trim();
        item.Description = req.Description;
        item.Year = req.Year;
        if (!string.IsNullOrEmpty(req.ImageData))
        {
            item.ImageData = req.ImageData;
            item.ImageType = req.ImageType ?? "image/jpeg";
        }
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<ActionResult> Delete(long id)
    {
        var item = await db.Seobe.FirstOrDefaultAsync(s => s.Id == id);
        if (item is null) return NotFound();
        db.Seobe.Remove(item);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
