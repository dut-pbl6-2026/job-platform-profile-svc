using Profile.Core.Entities;
using Xunit;

namespace Profile.Tests;

/// <summary>
/// Domain validation tests — entities throw on invalid state instead of
/// leaking late DbUpdateException (PROFILE-01-02/03/04).
/// </summary>
public class DomainValidationTests
{
    private static readonly Guid ProfileId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    // ── Skill ─────────────────────────────────────────────────────────────
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Skill_ValidProficiency_Creates(int proficiency)
    {
        var skill = new Skill(ProfileId, "C#", proficiency, 2);

        Assert.Equal(proficiency, skill.Proficiency);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void Skill_InvalidProficiency_Throws(int proficiency)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Skill(ProfileId, "C#", proficiency, 2));
    }

    [Fact]
    public void Skill_NegativeYearsOfExperience_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Skill(ProfileId, "C#", 3, -1));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Skill_BlankName_Throws(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => new Skill(ProfileId, name!, 3, 1));
    }

    [Fact]
    public void Skill_Update_AppliesAndTouches()
    {
        var skill = new Skill(ProfileId, "C#", 2, 1);
        var before = skill.UpdatedAt;

        skill.Update("Java", 4, 3);

        Assert.Equal("Java", skill.Name);
        Assert.Equal(4, skill.Proficiency);
        Assert.True(skill.UpdatedAt >= before);
    }

    // ── WorkExperience ────────────────────────────────────────────────────
    [Fact]
    public void WorkExperience_EndAfterStart_Creates()
    {
        var exp = new WorkExperience(ProfileId, "Acme", "Dev",
            new DateOnly(2022, 1, 1), new DateOnly(2023, 1, 1), false, null);

        Assert.Equal(new DateOnly(2023, 1, 1), exp.EndDate);
    }

    [Fact]
    public void WorkExperience_EndBeforeStart_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new WorkExperience(ProfileId, "Acme", "Dev",
                new DateOnly(2023, 1, 1), new DateOnly(2022, 1, 1), false, null));
    }

    [Fact]
    public void WorkExperience_CurrentWithoutEndDate_Creates()
    {
        var exp = new WorkExperience(ProfileId, "Acme", "Dev",
            new DateOnly(2022, 1, 1), null, true, null);

        Assert.True(exp.IsCurrent);
        Assert.Null(exp.EndDate);
    }

    [Fact]
    public void WorkExperience_Update_InvalidDates_Throws()
    {
        var exp = new WorkExperience(ProfileId, "Acme", "Dev",
            new DateOnly(2022, 1, 1), null, true, null);

        Assert.Throws<ArgumentException>(
            () => exp.Update("Acme", "Dev",
                new DateOnly(2023, 1, 1), new DateOnly(2022, 1, 1), false, null));
    }

    // ── Education ─────────────────────────────────────────────────────────
    [Fact]
    public void Education_EndBeforeStart_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new Education(ProfileId, "HUST", "BSc", "CS",
                new DateOnly(2023, 1, 1), new DateOnly(2022, 1, 1), null));
    }

    [Fact]
    public void Education_OngoingWithoutEndDate_Creates()
    {
        var edu = new Education(ProfileId, "HUST", "BSc", "CS",
            new DateOnly(2022, 1, 1), null, null);

        Assert.Null(edu.EndDate);
    }

    // ── UserProfile ───────────────────────────────────────────────────────
    [Fact]
    public void UserProfile_Create_SetsUserAndTrimsName()
    {
        var profile = new UserProfile(UserId, "  Nguyen Van A  ");

        Assert.Equal(UserId, profile.UserId);
        Assert.Equal("Nguyen Van A", profile.FullName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UserProfile_BlankFullName_Throws(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() => new UserProfile(UserId, name!));
    }

    [Fact]
    public void UserProfile_Update_ReplacesEncryptedFields()
    {
        var profile = new UserProfile(UserId, "A");

        profile.Update("B", "headline", "summary", "http://avatar", "enc-phone", null, null);

        Assert.Equal("B", profile.FullName);
        Assert.Equal("enc-phone", profile.PhoneEncrypted);
        Assert.Null(profile.AddressEncrypted);
    }
}
