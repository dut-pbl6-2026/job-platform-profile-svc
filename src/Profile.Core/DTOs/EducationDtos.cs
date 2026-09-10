using System.ComponentModel.DataAnnotations;

namespace Profile.Core.DTOs;

/// <summary>
/// DTO nhận dữ liệu từ client để thêm mục học vấn — PROFILE-01-04.
/// </summary>
public record EducationCreateDto
{
    /// <summary>Tên trường / cơ sở đào tạo — bắt buộc.</summary>
    [Required(ErrorMessage = "Institution is required.")]
    [MaxLength(256, ErrorMessage = "Institution must not exceed 256 characters.")]
    public string Institution { get; init; } = "";

    /// <summary>Bằng cấp (Cử nhân, Thạc sĩ, ...).</summary>
    [MaxLength(128, ErrorMessage = "Degree must not exceed 128 characters.")]
    public string? Degree { get; init; }

    /// <summary>Chuyên ngành.</summary>
    [MaxLength(128, ErrorMessage = "Field must not exceed 128 characters.")]
    public string? Field { get; init; }

    /// <summary>Ngày bắt đầu học.</summary>
    [Required(ErrorMessage = "StartDate is required.")]
    public DateOnly StartDate { get; init; }

    /// <summary>Ngày tốt nghiệp — null nếu đang học.</summary>
    public DateOnly? EndDate { get; init; }

    /// <summary>Điểm GPA hoặc xếp loại.</summary>
    [MaxLength(32, ErrorMessage = "Grade must not exceed 32 characters.")]
    public string? Grade { get; init; }
}

/// <summary>
/// DTO nhận dữ liệu từ client để cập nhật mục học vấn — PROFILE-01-04.
/// </summary>
public record EducationUpdateDto
{
    /// <summary>Tên trường / cơ sở đào tạo — bắt buộc.</summary>
    [Required(ErrorMessage = "Institution is required.")]
    [MaxLength(256, ErrorMessage = "Institution must not exceed 256 characters.")]
    public string Institution { get; init; } = "";

    /// <summary>Bằng cấp (Cử nhân, Thạc sĩ, ...).</summary>
    [MaxLength(128, ErrorMessage = "Degree must not exceed 128 characters.")]
    public string? Degree { get; init; }

    /// <summary>Chuyên ngành.</summary>
    [MaxLength(128, ErrorMessage = "Field must not exceed 128 characters.")]
    public string? Field { get; init; }

    /// <summary>Ngày bắt đầu học.</summary>
    [Required(ErrorMessage = "StartDate is required.")]
    public DateOnly StartDate { get; init; }

    /// <summary>Ngày tốt nghiệp — null nếu đang học.</summary>
    public DateOnly? EndDate { get; init; }

    /// <summary>Điểm GPA hoặc xếp loại.</summary>
    [MaxLength(32, ErrorMessage = "Grade must not exceed 32 characters.")]
    public string? Grade { get; init; }
}
