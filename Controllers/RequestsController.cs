using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PorodicnoStablo.Api.Data;
using PorodicnoStablo.Api.Dtos;
using PorodicnoStablo.Api.Entities;

namespace PorodicnoStablo.Api.Controllers;

[ApiController]
[Route("api/requests")]
[Authorize]
public class RequestsController(AppDbContext db) : ControllerBase
{
    private static readonly string[] AllowedTypes = ["member", "member_edit", "gallery", "history", "announcement"];

    [HttpGet]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<List<DataRequest>>> GetAll([FromQuery] string? status)
    {
        var q = db.DataRequests.AsNoTracking().OrderBy(r => r.CreatedAt).AsQueryable();
        if (!string.IsNullOrEmpty(status) && status != "all") q = q.Where(r => r.Status == status);
        return await q.ToListAsync();
    }

    [HttpGet("pending-count")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> PendingCount()
        => Ok(new { count = await db.DataRequests.CountAsync(r => r.Status == "pending") });

    /// <summary>
    /// Kreiranje zahtjeva — user_id/user_email se uzimaju IZ TOKENA (server-side),
    /// klijent više ne može podmetnuti tuđi identitet. Status je uvijek "pending".
    /// </summary>
    [HttpPost]
    public async Task<ActionResult> Create([FromBody] CreateDataRequest req)
    {
        if (!AllowedTypes.Contains(req.RequestType))
            return BadRequest(new { message = "Nepoznat tip zahtjeva." });

        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email");

        db.DataRequests.Add(new DataRequest
        {
            RequestType = req.RequestType,
            UserId = Guid.TryParse(sub, out var uid) ? uid : null,
            UserEmail = email,
            Status = "pending",
            FirstName = req.FirstName,
            LastName = req.LastName,
            Gender = req.Gender,
            BirthYear = req.BirthYear,
            DeathYear = req.DeathYear,
            Notes = req.Notes,
            ParentIds = req.ParentIds,
            GenerationalLine = req.GenerationalLine,
            SpouseId = req.SpouseId,
            SpouseName = req.SpouseName,
            EditedMemberId = req.EditedMemberId,
            Title = req.Title,
            Content = req.Content,
            ImageData = req.ImageData,
            ImageType = req.ImageType,
            PhotoYear = req.PhotoYear,
            StoryDate = req.StoryDate,
            HavePdf = req.HavePdf,
            PdfPath = req.PdfPath,
            ExpiresAt = req.ExpiresAt,
        });
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Odluka admina — odobrenje IZVRŠAVA efekat (insert člana/slike/priče/obavještenja)
    /// i update status zahtjeva u JEDNOJ transakciji. Ranije je ovo bilo 3-4 odvojena
    /// poziva iz browsera koji su mogli ostaviti bazu u pola-odrađenom stanju.
    /// </summary>
    [HttpPost("{id:long}/decision")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> Decide(long id, [FromBody] DecisionRequest body)
    {
        if (body.Decision is not ("approved" or "rejected"))
            return BadRequest(new { message = "Odluka mora biti 'approved' ili 'rejected'." });

        await using var tx = await db.Database.BeginTransactionAsync();

        var req = await db.DataRequests.FirstOrDefaultAsync(r => r.Id == id);
        if (req is null) return NotFound();
        if (req.Status != "pending") return Conflict(new { message = "Zahtjev je već riješen." });

        if (body.Decision == "approved")
            await ApplyApproval(req);

        req.Status = body.Decision;
        req.AdminNote = body.AdminNote;
        req.ReviewedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return NoContent();
    }

    [HttpDelete("resolved")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> ClearResolved()
    {
        await db.DataRequests
            .Where(r => r.Status == "approved" || r.Status == "rejected")
            .ExecuteDeleteAsync();
        return NoContent();
    }

    private async Task ApplyApproval(DataRequest req)
    {
        switch (req.RequestType)
        {
            case "member":
            {
                var member = new Member
                {
                    FirstName = req.FirstName ?? "",
                    LastName = req.LastName,
                    Gender = req.Gender,
                    BirthYear = req.BirthYear,
                    DeathYear = req.DeathYear,
                    Notes = req.Notes,
                    SpouseId = req.SpouseId,
                    SpouseName = req.SpouseName,
                    GenerationalLine = req.GenerationalLine,
                    Featured = false,
                };
                db.Members.Add(member);
                await db.SaveChangesAsync(); // treba nam id za veze roditelja

                foreach (var pid in (req.ParentIds ?? []).Distinct())
                    db.MemberParents.Add(new MemberParent { MemberId = member.Id, ParentId = pid });
                break;
            }
            case "member_edit":
            {
                var member = await db.Members.FirstOrDefaultAsync(m => m.Id == req.EditedMemberId);
                if (member is null) throw new InvalidOperationException("Član za izmjenu ne postoji.");
                member.FirstName = req.FirstName ?? member.FirstName;
                member.LastName = req.LastName;
                member.BirthYear = req.BirthYear;
                member.DeathYear = req.DeathYear;
                member.Notes = req.Notes;
                break;
            }
            case "gallery":
                db.Gallery.Add(new GalleryItem
                {
                    Title = req.Title ?? "",
                    Description = req.Content,
                    ImageData = req.ImageData,
                    ImageType = req.ImageType ?? "image/jpeg",
                    PhotoYear = req.PhotoYear,
                    CreatedBy = req.UserId,
                });
                break;

            case "history":
                db.HistoryStories.Add(new HistoryStory
                {
                    Title = req.Title ?? "",
                    Content = req.Content ?? "",
                    CoverImage = req.ImageData,
                    ImageType = req.ImageType ?? "image/jpeg",
                    StoryDate = req.StoryDate,
                    HavePdf = req.HavePdf,
                    PdfPath = req.HavePdf ? req.PdfPath : null,
                    CreatedBy = req.UserId,
                });
                break;

            case "announcement":
                db.Announcements.Add(new Announcement
                {
                    Message = req.Content ?? "",
                    ExpiresAt = req.ExpiresAt ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)),
                    CreatedBy = req.UserId,
                });
                break;
        }
    }
}
