using SharedKernel;

namespace Profile.Core.Entities;

/// <summary>
/// Lịch sử giáo dục của ứng viên — PROFILE-01-04 (SRS 3.6.4).
/// Cho phép nhiều mục giáo dục trên mỗi hồ sơ.
/// </summary>
public class Education : Entity
{
    /// <summary>Độ dài tối đa — đồng bộ với DTO [MaxLength] và DB varchar (institution 256, degree/field 128, grade 32).</summary>
    public const int MaxInstitutionLength = 256;
    public const int MaxDegreeLength = 128;
    public const int MaxFieldLength = 128;
    public const int MaxGradeLength = 32;

    /// <summary>FK đến UserProfile.</summary>
    public Guid ProfileId { get; private set; }

    /// <summary>Navigation ngược lên profile.</summary>
    public UserProfile? Profile { get; private set; }

    /// <summary>Tên trường / cơ sở đào tạo — max 256, required.</summary>
    public string Institution { get; private set; } = "";

    /// <summary>Bằng cấp — max 128, nullable (Cử nhân, Thạc sĩ, ...).</summary>
    public string? Degree { get; private set; }

    /// <summary>Chuyên ngành — max 128, nullable.</summary>
    public string? Field { get; private set; }

    /// <summary>Ngày bắt đầu học.</summary>
    public DateOnly StartDate { get; private set; }

    /// <summary>Ngày tốt nghiệp — null nếu đang học.</summary>
    public DateOnly? EndDate { get; private set; }

    /// <summary>Điểm GPA hoặc xếp loại — max 32, nullable.</summary>
    public string? Grade { get; private set; }

    // EF Core constructor
    private Education() { }

    /// <summary>Thêm mục giáo dục mới.</summary>
    public Education(
        Guid profileId,
        string institution,
        string? degree,
        string? field,
        DateOnly startDate,
        DateOnly? endDate,
        string? grade)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(institution, nameof(institution));
        ValidateLengths(institution, degree, field, grade);
        if (endDate.HasValue && endDate.Value < startDate)
            throw new ArgumentException("EndDate must be >= StartDate.", nameof(endDate));

        ProfileId = profileId;
        Institution = institution.Trim();
        Degree = degree?.Trim();
        Field = field?.Trim();
        StartDate = startDate;
        EndDate = endDate;
        Grade = grade?.Trim();
    }

    /// <summary>Cập nhật thông tin giáo dục.</summary>
    public void Update(
        string institution,
        string? degree,
        string? field,
        DateOnly startDate,
        DateOnly? endDate,
        string? grade)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(institution, nameof(institution));
        ValidateLengths(institution, degree, field, grade);
        if (endDate.HasValue && endDate.Value < startDate)
            throw new ArgumentException("EndDate must be >= StartDate.", nameof(endDate));

        Institution = institution.Trim();
        Degree = degree?.Trim();
        Field = field?.Trim();
        StartDate = startDate;
        EndDate = endDate;
        Grade = grade?.Trim();
        Touch();
    }

    private static void ValidateLengths(string institution, string? degree, string? field, string? grade)
    {
        if (institution.Trim().Length > MaxInstitutionLength)
            throw new ArgumentException($"Institution must not exceed {MaxInstitutionLength} characters.", nameof(institution));
        if (degree is not null && degree.Trim().Length > MaxDegreeLength)
            throw new ArgumentException($"Degree must not exceed {MaxDegreeLength} characters.", nameof(degree));
        if (field is not null && field.Trim().Length > MaxFieldLength)
            throw new ArgumentException($"Field must not exceed {MaxFieldLength} characters.", nameof(field));
        if (grade is not null && grade.Trim().Length > MaxGradeLength)
            throw new ArgumentException($"Grade must not exceed {MaxGradeLength} characters.", nameof(grade));
    }
}
