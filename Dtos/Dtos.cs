namespace PorodicnoStablo.Api.Dtos;

// ── Members ──────────────────────────────────────────────────────────────────

/// <summary>Član sa listom roditelja — ekvivalent nekadašnjeg view-a members_with_parents.</summary>
public record MemberDto(
    long Id, string FirstName, string? LastName, string? Gender,
    int? BirthYear, int? DeathYear, string? Notes,
    long? SpouseId, string? SpouseName, int? GenerationalLine,
    bool Featured, string? FeaturedNote, List<long> ParentIds);

/// <summary>Kreiranje/izmjena člana — roditelji i djeca se sinhronizuju u jednoj transakciji.</summary>
public class SaveMemberRequest
{
    public string FirstName { get; set; } = "";
    public string? LastName { get; set; }
    public string? Gender { get; set; }
    public int? BirthYear { get; set; }
    public int? DeathYear { get; set; }
    public string? Notes { get; set; }
    public long? SpouseId { get; set; }
    public string? SpouseName { get; set; }
    public int? GenerationalLine { get; set; }
    public bool Featured { get; set; }
    public string? FeaturedNote { get; set; }
    public List<long>? ParentIds { get; set; }
    public List<long>? ChildIds { get; set; }
}

// ── Announcements ────────────────────────────────────────────────────────────

public class SaveAnnouncementRequest
{
    public string Message { get; set; } = "";
    public DateOnly ExpiresAt { get; set; }
}

// ── Gallery ──────────────────────────────────────────────────────────────────

public record GalleryItemDto(
    long Id, string Title, string? Description, int? PhotoYear,
    string? ImageType, bool HasImage);

public class SaveGalleryRequest
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public int? PhotoYear { get; set; }
    /// <summary>Base64. Null pri izmjeni = zadrži postojeću sliku.</summary>
    public string? ImageData { get; set; }
    public string? ImageType { get; set; }
}

// ── History ──────────────────────────────────────────────────────────────────

public record HistoryStoryDto(
    long Id, string Title, string Content, DateOnly? StoryDate,
    string? ImageType, bool HasCover, bool HavePdf, string? PdfPath, string? PdfUrl,
    DateTime CreatedAt);

public class SaveHistoryRequest
{
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public DateOnly? StoryDate { get; set; }
    /// <summary>Base64 naslovne slike. Null pri izmjeni = zadrži postojeću.</summary>
    public string? CoverImage { get; set; }
    public string? ImageType { get; set; }
    public bool HavePdf { get; set; }
    public string? PdfPath { get; set; }
    /// <summary>Eksplicitno uklanjanje slike pri izmjeni.</summary>
    public bool RemoveCover { get; set; }
}

// ── Seobe ────────────────────────────────────────────────────────────────────

public record SeobaDto(long Id, string Title, string? Description, int Year, string? ImageType, bool HasImage);

public class SaveSeobaRequest
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public int Year { get; set; }
    /// <summary>Base64. Null pri izmjeni = zadrži postojeću sliku.</summary>
    public string? ImageData { get; set; }
    public string? ImageType { get; set; }
}

// ── Data requests ────────────────────────────────────────────────────────────

public class CreateDataRequest
{
    public string RequestType { get; set; } = "member";
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Gender { get; set; }
    public int? BirthYear { get; set; }
    public int? DeathYear { get; set; }
    public string? Notes { get; set; }
    public List<long>? ParentIds { get; set; }
    public int? GenerationalLine { get; set; }
    public long? SpouseId { get; set; }
    public string? SpouseName { get; set; }
    public long? EditedMemberId { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string? ImageData { get; set; }
    public string? ImageType { get; set; }
    public int? PhotoYear { get; set; }
    public DateOnly? StoryDate { get; set; }
    public bool HavePdf { get; set; }
    public string? PdfPath { get; set; }
    public DateOnly? ExpiresAt { get; set; }
}

public class DecisionRequest
{
    /// <summary>"approved" ili "rejected"</summary>
    public string Decision { get; set; } = "";
    public string? AdminNote { get; set; }
}

public record MeDto(Guid Id, string? Email, string? Role, string? FullName);
