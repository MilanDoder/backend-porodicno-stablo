using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PorodicnoStablo.Api.Entities;

namespace PorodicnoStablo.Api.Data;

/// <summary>
/// Mapira se na POSTOJEĆU Supabase Postgres šemu (public schema, snake_case kolone).
/// Nema migracija — baza ostaje netaknuta.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Member> Members => Set<Member>();
    public DbSet<MemberParent> MemberParents => Set<MemberParent>();
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<GalleryItem> Gallery => Set<GalleryItem>();
    public DbSet<HistoryStory> HistoryStories => Set<HistoryStory>();
    public DbSet<Seoba> Seobe => Set<Seoba>();
    public DbSet<DataRequest> DataRequests => Set<DataRequest>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Member>(e =>
        {
            e.ToTable("members");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.FirstName).HasColumnName("first_name");
            e.Property(x => x.LastName).HasColumnName("last_name");
            e.Property(x => x.Gender).HasColumnName("gender");
            e.Property(x => x.BirthYear).HasColumnName("birth_year");
            e.Property(x => x.DeathYear).HasColumnName("death_year");
            e.Property(x => x.Notes).HasColumnName("notes");
            e.Property(x => x.SpouseId).HasColumnName("spouse_id");
            e.Property(x => x.SpouseName).HasColumnName("spouse_name");
            e.Property(x => x.GenerationalLine).HasColumnName("generational_line");
            e.Property(x => x.Featured).HasColumnName("featured");
            e.Property(x => x.FeaturedNote).HasColumnName("featured_note");
        });

        b.Entity<MemberParent>(e =>
        {
            e.ToTable("member_parents");
            e.HasKey(x => new { x.MemberId, x.ParentId });
            e.Property(x => x.MemberId).HasColumnName("member_id");
            e.Property(x => x.ParentId).HasColumnName("parent_id");
        });

        b.Entity<Profile>(e =>
        {
            e.ToTable("profiles");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Role).HasColumnName("role");
            e.Property(x => x.FullName).HasColumnName("full_name");
        });

        b.Entity<Announcement>(e =>
        {
            e.ToTable("announcements");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Message).HasColumnName("message");
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").ValueGeneratedOnAdd();
        });

        b.Entity<GalleryItem>(e =>
        {
            e.ToTable("gallery");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Title).HasColumnName("title");
            e.Property(x => x.Description).HasColumnName("description");
            e.Property(x => x.ImageData).HasColumnName("image_data");
            e.Property(x => x.ImageType).HasColumnName("image_type");
            e.Property(x => x.PhotoYear).HasColumnName("photo_year");
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
        });

        b.Entity<HistoryStory>(e =>
        {
            e.ToTable("history_stories");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Title).HasColumnName("title");
            e.Property(x => x.Content).HasColumnName("content");
            e.Property(x => x.CoverImage).HasColumnName("cover_image");
            e.Property(x => x.ImageType).HasColumnName("image_type");
            e.Property(x => x.StoryDate).HasColumnName("story_date");
            e.Property(x => x.HavePdf).HasColumnName("have_pdf");
            e.Property(x => x.PdfPath).HasColumnName("pdf_path");
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").ValueGeneratedOnAdd();
        });

        b.Entity<Seoba>(e =>
        {
            e.ToTable("seobe");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Title).HasColumnName("title");
            e.Property(x => x.Description).HasColumnName("description");
            e.Property(x => x.Year).HasColumnName("year");
            e.Property(x => x.ImageData).HasColumnName("image_data");
            e.Property(x => x.ImageType).HasColumnName("image_type");
        });

        b.Entity<DataRequest>(e =>
        {
            e.ToTable("data_requests");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.RequestType).HasColumnName("request_type");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.UserEmail).HasColumnName("user_email");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.AdminNote).HasColumnName("admin_note");
            e.Property(x => x.ReviewedAt).HasColumnName("reviewed_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").ValueGeneratedOnAdd();
            e.Property(x => x.FirstName).HasColumnName("first_name");
            e.Property(x => x.LastName).HasColumnName("last_name");
            e.Property(x => x.Gender).HasColumnName("gender");
            e.Property(x => x.BirthYear).HasColumnName("birth_year");
            e.Property(x => x.DeathYear).HasColumnName("death_year");
            e.Property(x => x.Notes).HasColumnName("notes");
            e.Property(x => x.GenerationalLine).HasColumnName("generational_line");
            e.Property(x => x.SpouseId).HasColumnName("spouse_id");
            e.Property(x => x.SpouseName).HasColumnName("spouse_name");
            e.Property(x => x.EditedMemberId).HasColumnName("edited_member_id");
            e.Property(x => x.Title).HasColumnName("title");
            e.Property(x => x.Content).HasColumnName("content");
            e.Property(x => x.ImageData).HasColumnName("image_data");
            e.Property(x => x.ImageType).HasColumnName("image_type");
            e.Property(x => x.PhotoYear).HasColumnName("photo_year");
            e.Property(x => x.StoryDate).HasColumnName("story_date");
            e.Property(x => x.HavePdf).HasColumnName("have_pdf");
            e.Property(x => x.PdfPath).HasColumnName("pdf_path");
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");

            // parent_ids: supabase-js šalje JS niz → pretpostavljena kolona jsonb.
            // Ako je kolona tipa int8[]/int4[], vidi napomenu u README (mapiranje u jednoj liniji).
            var jsonOpts = (JsonSerializerOptions?)null;
            e.Property(x => x.ParentIds)
                .HasColumnName("parent_ids")
                .HasColumnType("jsonb")
                .HasConversion(
                    new ValueConverter<List<long>?, string?>(
                        v => v == null ? null : JsonSerializer.Serialize(v, jsonOpts),
                        v => v == null ? null : JsonSerializer.Deserialize<List<long>>(v, jsonOpts)),
                    new ValueComparer<List<long>?>(
                        (a, c) => (a == null && c == null) || (a != null && c != null && a.SequenceEqual(c)),
                        v => v == null ? 0 : v.Aggregate(0, (h, i) => HashCode.Combine(h, i.GetHashCode())),
                        v => v == null ? null : v.ToList()));
        });
    }
}
