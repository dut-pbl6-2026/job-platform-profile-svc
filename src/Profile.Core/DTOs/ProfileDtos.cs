namespace Profile.Core.DTOs;

/// <summary>
/// DTO mô tả một kỹ năng trong danh sách hồ sơ.
/// </summary>
public record SkillDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public int Proficiency { get; init; }
    public int YearsOfExperience { get; init; }
}

/// <summary>
/// DTO mô tả một mục kinh nghiệm làm việc trong hồ sơ.
/// </summary>
public record WorkExperienceDto
{
    public Guid Id { get; init; }
    public string Company { get; init; } = "";
    public string Title { get; init; } = "";
    public DateOnly StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public bool IsCurrent { get; init; }
    public string? Description { get; init; }
}

/// <summary>
/// DTO mô tả một mục học vấn trong hồ sơ.
/// </summary>
public record EducationDto
{
    public Guid Id { get; init; }
    public string Institution { get; init; } = "";
    public string? Degree { get; init; }
    public string? Field { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public string? Grade { get; init; }
}

/// <summary>
/// DTO chi tiết hồ sơ cá nhân đầy đủ — chỉ trả về cho chính chủ (PROFILE-01-05).
/// Bao gồm PII đã giải mã (phone, address, dateOfBirth) — SEC-08.
/// </summary>
public record ProfileDetailDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string FullName { get; init; } = "";
    public string? Headline { get; init; }
    public string? Summary { get; init; }
    public string? AvatarUrl { get; init; }

    /// <summary>Số điện thoại đã giải mã — SEC-08, chỉ dành cho chính chủ.</summary>
    public string? Phone { get; init; }

    /// <summary>Địa chỉ đã giải mã — SEC-08, chỉ dành cho chính chủ.</summary>
    public string? Address { get; init; }

    /// <summary>Ngày sinh đã giải mã — SEC-08, chỉ dành cho chính chủ.</summary>
    public string? DateOfBirth { get; init; }

    public IReadOnlyList<SkillDto> Skills { get; init; } = [];
    public IReadOnlyList<WorkExperienceDto> Experiences { get; init; } = [];
    public IReadOnlyList<EducationDto> Educations { get; init; } = [];
}

/// <summary>
/// DTO hồ sơ công khai — dành cho nhà tuyển dụng và người dùng khác (PROFILE-01-06).
/// TUYỆT ĐỐI không bao gồm phone, address, dateOfBirth (SEC-08).
/// </summary>
public record PublicProfileDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string FullName { get; init; } = "";
    public string? Headline { get; init; }
    public string? Summary { get; init; }
    public string? AvatarUrl { get; init; }

    public IReadOnlyList<SkillDto> Skills { get; init; } = [];
    public IReadOnlyList<WorkExperienceDto> Experiences { get; init; } = [];
    public IReadOnlyList<EducationDto> Educations { get; init; } = [];
}
