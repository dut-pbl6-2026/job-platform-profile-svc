using Profile.Core.DTOs;
using Xunit;

namespace Profile.Tests;

/// <summary>
/// Contract pins for the profile DTOs (PROFILE-01-05/06, SEC-08). A change to a property set
/// must be deliberate: it fails here first. No database is needed.
/// </summary>
public class ProfileDtoContractTests
{
    private static IReadOnlyList<string> PropertyNames(Type type) =>
        type.GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();

    private static IReadOnlyList<string> Sorted(params string[] names) =>
        names.OrderBy(n => n, StringComparer.Ordinal).ToList();

    [Fact]
    public void PublicProfileDto_ExposesExactlyThePiiFreeShape()
    {
        Assert.Equal(
            Sorted("Id", "UserId", "FullName", "Headline", "Summary", "AvatarUrl", "Skills", "Experiences", "Educations"),
            PropertyNames(typeof(PublicProfileDto)));
    }

    [Fact]
    public void PublicProfileDto_HasNoSensitiveFields()
    {
        var names = PropertyNames(typeof(PublicProfileDto));

        Assert.DoesNotContain("Phone", names);
        Assert.DoesNotContain("Address", names);
        Assert.DoesNotContain("DateOfBirth", names);
    }

    [Fact]
    public void ProfileDetailDto_ExposesTheOwnerShape()
    {
        Assert.Equal(
            Sorted("Id", "UserId", "FullName", "Headline", "Summary", "AvatarUrl",
                   "Phone", "Address", "DateOfBirth", "Skills", "Experiences", "Educations"),
            PropertyNames(typeof(ProfileDetailDto)));
    }

    [Fact]
    public void ProfileUpdateDto_AcceptsTheDocumentedFields()
    {
        Assert.Equal(
            Sorted("FullName", "Headline", "Summary", "AvatarUrl", "Phone", "Address", "DateOfBirth"),
            PropertyNames(typeof(ProfileUpdateDto)));
    }

    [Fact]
    public void ChildDtos_KeepTheirShape()
    {
        Assert.Equal(Sorted("Id", "Name", "Proficiency", "YearsOfExperience"), PropertyNames(typeof(SkillDto)));
        Assert.Equal(
            Sorted("Id", "Company", "Title", "StartDate", "EndDate", "IsCurrent", "Description"),
            PropertyNames(typeof(WorkExperienceDto)));
        Assert.Equal(
            Sorted("Id", "Institution", "Degree", "Field", "StartDate", "EndDate", "Grade"),
            PropertyNames(typeof(EducationDto)));
    }
}
