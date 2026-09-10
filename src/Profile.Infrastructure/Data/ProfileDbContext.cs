using Microsoft.EntityFrameworkCore;
using Profile.Core.Entities;

namespace Profile.Infrastructure.Data;

/// <summary>
/// EF Core DbContext cho Profile Service — DB job_platform_profile (SEC-08, SRS DB-01-01).
/// Cấu hình Fluent API: Unique index UserId, check constraint proficiency, cascade delete cho sub-entities.
/// </summary>
public class ProfileDbContext : DbContext
{
    public ProfileDbContext(DbContextOptions<ProfileDbContext> options) : base(options) { }

    public DbSet<UserProfile> Profiles => Set<UserProfile>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<WorkExperience> WorkExperiences => Set<WorkExperience>();
    public DbSet<Education> Educations => Set<Education>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // ── UserProfile ──────────────────────────────────────────────────────────
        b.Entity<UserProfile>(e =>
        {
            e.ToTable("profiles");
            e.HasKey(x => x.Id);

            // 1:1 logic với Auth Service — UserId phải unique
            e.HasIndex(x => x.UserId).IsUnique();
            e.Property(x => x.UserId).IsRequired();

            e.Property(x => x.FullName).HasMaxLength(128).IsRequired();
            e.Property(x => x.Headline).HasMaxLength(256);
            e.Property(x => x.Summary).HasMaxLength(2000);
            e.Property(x => x.AvatarUrl).HasMaxLength(512);

            // SEC-08: PII fields stored as ciphertext — no length limit for Base64(Nonce+Tag+Cipher)
            e.Property(x => x.PhoneEncrypted).HasColumnName("phone");
            e.Property(x => x.AddressEncrypted).HasColumnName("address");
            e.Property(x => x.DateOfBirthEncrypted).HasColumnName("date_of_birth");

            // Cascade delete sub-entities when profile is deleted
            e.HasMany(x => x.Skills)
                .WithOne(x => x.Profile)
                .HasForeignKey(x => x.ProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(x => x.Experiences)
                .WithOne(x => x.Profile)
                .HasForeignKey(x => x.ProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(x => x.Educations)
                .WithOne(x => x.Profile)
                .HasForeignKey(x => x.ProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Skill ────────────────────────────────────────────────────────────────
        b.Entity<Skill>(e =>
        {
            e.ToTable("skills");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Proficiency).HasColumnName("proficiency").IsRequired();
            e.Property(x => x.YearsOfExperience).HasColumnName("years_of_experience").IsRequired();

            // DB-level check constraints (domain already validates, defense in depth).
            // NOTE: SQL references snake_case columns — keep HasColumnName in sync.
            e.ToTable(t => t.HasCheckConstraint("CK_skills_proficiency", "proficiency >= 1 AND proficiency <= 5"));
            e.ToTable(t => t.HasCheckConstraint("CK_skills_years", "years_of_experience >= 0"));

            e.HasIndex(x => x.ProfileId);
        });

        // ── WorkExperience ───────────────────────────────────────────────────────
        b.Entity<WorkExperience>(e =>
        {
            e.ToTable("work_experiences");
            e.HasKey(x => x.Id);
            e.Property(x => x.Company).HasMaxLength(256).IsRequired();
            e.Property(x => x.Title).HasMaxLength(128).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.IsCurrent).HasDefaultValue(false);
            e.HasIndex(x => x.ProfileId);
        });

        // ── Education ────────────────────────────────────────────────────────────
        b.Entity<Education>(e =>
        {
            e.ToTable("educations");
            e.HasKey(x => x.Id);
            e.Property(x => x.Institution).HasMaxLength(256).IsRequired();
            e.Property(x => x.Degree).HasMaxLength(128);
            e.Property(x => x.Field).HasMaxLength(128);
            e.Property(x => x.Grade).HasMaxLength(32);
            e.HasIndex(x => x.ProfileId);
        });
    }
}
