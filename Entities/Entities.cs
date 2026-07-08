namespace PorodicnoStablo.Api.Entities;

public class Member
{
    public long Id { get; set; }
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
}

public class MemberParent
{
    public long MemberId { get; set; }
    public long ParentId { get; set; }
}

public class Profile
{
    public Guid Id { get; set; }
    public string? Role { get; set; }
    public string? FullName { get; set; }
}

public class Announcement
{
    public long Id { get; set; }
    public string Message { get; set; } = "";
    public DateOnly ExpiresAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class GalleryItem
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string? ImageData { get; set; }   // base64 u bazi (postojeća šema)
    public string? ImageType { get; set; }
    public int? PhotoYear { get; set; }
    public Guid? CreatedBy { get; set; }
}

public class HistoryStory
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string? CoverImage { get; set; }  // base64 u bazi (postojeća šema)
    public string? ImageType { get; set; }
    public DateOnly? StoryDate { get; set; }
    public bool HavePdf { get; set; }
    public string? PdfPath { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class Seoba
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public int Year { get; set; }
    public string? ImageData { get; set; }   // base64 u bazi (postojeća šema)
    public string? ImageType { get; set; }
}

public class DataRequest
{
    public long Id { get; set; }
    public string RequestType { get; set; } = "member";
    public Guid? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string Status { get; set; } = "pending";
    public string? AdminNote { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    // member / member_edit
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

    // gallery / history / announcement
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
