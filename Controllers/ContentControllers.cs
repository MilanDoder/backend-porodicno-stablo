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

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> Update(Guid id, [FromBody] SaveAnnouncementRequest req)
    {
        var ann = await db.Announcements.FirstOrDefaultAsync(a => a.Id == id);
        if (ann is null) return NotFound();
        ann.Message = req.Message.Trim();
        ann.ExpiresAt = req.ExpiresAt;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> Delete(Guid id)
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
