using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PorodicnoStablo.Api.Data;
using PorodicnoStablo.Api.Dtos;
using PorodicnoStablo.Api.Entities;

namespace PorodicnoStablo.Api.Controllers;

[ApiController]
[Route("api/me")]
[Authorize]
public class MeController(AppDbContext db) : ControllerBase
{
    /// <summary>Profil trenutno ulogovanog korisnika (rola dolazi iz baze, ne iz klijenta).</summary>
    [HttpGet]
    public async Task<ActionResult<MeDto>> Get()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var userId)) return Unauthorized();
        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email");
        var profile = await db.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == userId);
        return new MeDto(userId, email, profile?.Role ?? "user", profile?.FullName);
    }
}

[ApiController]
[Route("api/members")]
[Authorize]
public class MembersController(AppDbContext db) : ControllerBase
{
    /// <summary>Svi članovi sa parent_ids — zamjena za view members_with_parents, jedan upit + join.</summary>
    [HttpGet]
    public async Task<ActionResult<List<MemberDto>>> GetAll()
    {
        var members = await db.Members.AsNoTracking().OrderBy(m => m.Id).ToListAsync();
        var links = await db.MemberParents.AsNoTracking().ToListAsync();
        var parentMap = links.GroupBy(l => l.MemberId)
                             .ToDictionary(g => g.Key, g => g.Select(l => l.ParentId).ToList());

        return members.Select(m => new MemberDto(
            m.Id, m.FirstName, m.LastName, m.Gender, m.BirthYear, m.DeathYear, m.Notes,
            m.SpouseId, m.SpouseName, m.GenerationalLine, m.Featured, m.FeaturedNote,
            parentMap.TryGetValue(m.Id, out var p) ? p : []
        )).ToList();
    }

    /// <summary>Kreiranje člana + roditelji + djeca — sve u JEDNOJ transakciji (ranije 5+ poziva iz browsera).</summary>
    [HttpPost]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> Create([FromBody] SaveMemberRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.FirstName)) return BadRequest(new { message = "Ime je obavezno." });

        await using var tx = await db.Database.BeginTransactionAsync();

        var member = new Member();
        Apply(member, req);
        db.Members.Add(member);
        await db.SaveChangesAsync();

        await SyncParents(member.Id, req.ParentIds);
        await SyncChildren(member.Id, req.ChildIds);

        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return CreatedAtAction(nameof(GetAll), new { id = member.Id }, new { id = member.Id });
    }

    /// <summary>Izmjena člana + sinhronizacija roditelja i djece u jednoj transakciji.</summary>
    [HttpPut("{id:long}")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> Update(long id, [FromBody] SaveMemberRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.FirstName)) return BadRequest(new { message = "Ime je obavezno." });

        await using var tx = await db.Database.BeginTransactionAsync();

        var member = await db.Members.FirstOrDefaultAsync(m => m.Id == id);
        if (member is null) return NotFound();

        Apply(member, req);

        // Zamijeni roditelje
        var oldParents = await db.MemberParents.Where(l => l.MemberId == id).ToListAsync();
        db.MemberParents.RemoveRange(oldParents);
        await SyncParents(id, req.ParentIds);

        // Sinhronizuj djecu: dodaj nove veze, ukloni one koje više nisu odabrane
        await SyncChildren(id, req.ChildIds, removeMissing: true);

        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> Delete(long id)
    {
        await using var tx = await db.Database.BeginTransactionAsync();

        var links = await db.MemberParents.Where(l => l.MemberId == id || l.ParentId == id).ToListAsync();
        db.MemberParents.RemoveRange(links);

        var member = await db.Members.FirstOrDefaultAsync(m => m.Id == id);
        if (member is not null) db.Members.Remove(member);

        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return NoContent();
    }

    private static void Apply(Member m, SaveMemberRequest req)
    {
        m.FirstName = req.FirstName.Trim();
        m.LastName = req.LastName;
        m.Gender = req.Gender;
        m.BirthYear = req.BirthYear;
        m.DeathYear = req.DeathYear;
        m.Notes = req.Notes;
        m.SpouseId = req.SpouseId;
        m.SpouseName = req.SpouseName;
        m.GenerationalLine = req.GenerationalLine;
        m.Featured = req.Featured;
        m.FeaturedNote = req.FeaturedNote;
    }

    private async Task SyncParents(long memberId, List<long>? parentIds)
    {
        foreach (var pid in (parentIds ?? []).Distinct())
            db.MemberParents.Add(new MemberParent { MemberId = memberId, ParentId = pid });
        await Task.CompletedTask;
    }

    private async Task SyncChildren(long memberId, List<long>? childIds, bool removeMissing = false)
    {
        var wanted = (childIds ?? []).Distinct().ToHashSet();
        var existing = await db.MemberParents.Where(l => l.ParentId == memberId).ToListAsync();

        foreach (var link in existing.Where(l => removeMissing && !wanted.Contains(l.MemberId)))
            db.MemberParents.Remove(link);

        var existingIds = existing.Select(l => l.MemberId).ToHashSet();
        foreach (var cid in wanted.Where(c => !existingIds.Contains(c)))
            db.MemberParents.Add(new MemberParent { MemberId = cid, ParentId = memberId });
    }
}
