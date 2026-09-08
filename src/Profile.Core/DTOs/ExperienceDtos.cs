using System.ComponentModel.DataAnnotations;

namespace Profile.Core.DTOs;

/// <summary>
/// DTO nhận dữ liệu từ client để thêm kinh nghiệm làm việc — PROFILE-01-03.
/// </summary>
public record ExperienceCreateDto
{
    /// <summary>Tên công ty — bắt buộc.</summary>
    [Required(ErrorMessage = "Company is required.")]
    [MaxLength(256, ErrorMessage = "Company must not exceed 256 characters.")]
    public string Company { get; init; } = "";

    /// <summary>Chức danh — bắt buộc.</summary>
    [Required(ErrorMessage = "Title is required.")]
    [MaxLength(128, ErrorMessage = "Title must not exceed 128 characters.")]
    public string Title { get; init; } = "";

    /// <summary>Ngày bắt đầu.</summary>
    [Required(ErrorMessage = "StartDate is required.")]
    public DateOnly StartDate { get; init; }

    /// <summary>Ngày kết thúc — null nếu IsCurrent = true.</summary>
    public DateOnly? EndDate { get; init; }

    /// <summary>Có đang làm việc tại đây không.</summary>
    public bool IsCurrent { get; init; }

    /// <summary>Mô tả công việc.</summary>
    [MaxLength(2000, ErrorMessage = "Description must not exceed 2000 characters.")]
    public string? Description { get; init; }
}

/// <summary>
/// DTO nhận dữ liệu từ client để cập nhật kinh nghiệm làm việc — PROFILE-01-03.
/// </summary>
public record ExperienceUpdateDto
{
    /// <summary>Tên công ty — bắt buộc.</summary>
    [Required(ErrorMessage = "Company is required.")]
    [MaxLength(256, ErrorMessage = "Company must not exceed 256 characters.")]
    public string Company { get; init; } = "";

    /// <summary>Chức danh — bắt buộc.</summary>
    [Required(ErrorMessage = "Title is required.")]
    [MaxLength(128, ErrorMessage = "Title must not exceed 128 characters.")]
    public string Title { get; init; } = "";

    /// <summary>Ngày bắt đầu.</summary>
    [Required(ErrorMessage = "StartDate is required.")]
    public DateOnly StartDate { get; init; }

    /// <summary>Ngày kết thúc — null nếu IsCurrent = true.</summary>
    public DateOnly? EndDate { get; init; }

    /// <summary>Có đang làm việc tại đây không.</summary>
    public bool IsCurrent { get; init; }

    /// <summary>Mô tả công việc.</summary>
    [MaxLength(2000, ErrorMessage = "Description must not exceed 2000 characters.")]
    public string? Description { get; init; }
}
