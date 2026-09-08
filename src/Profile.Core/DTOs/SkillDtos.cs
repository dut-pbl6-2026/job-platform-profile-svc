using System.ComponentModel.DataAnnotations;

namespace Profile.Core.DTOs;

/// <summary>
/// DTO nhận dữ liệu từ client để thêm kỹ năng mới — PROFILE-01-02.
/// </summary>
public record SkillCreateDto
{
    /// <summary>Tên kỹ năng — bắt buộc, tối đa 100 ký tự.</summary>
    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name must not exceed 100 characters.")]
    public string Name { get; init; } = "";

    /// <summary>Mức độ thành thạo từ 1 (Beginner) đến 5 (Expert).</summary>
    [Range(1, 5, ErrorMessage = "Proficiency must be between 1 and 5.")]
    public int Proficiency { get; init; }

    /// <summary>Số năm kinh nghiệm (>= 0).</summary>
    [Range(0, int.MaxValue, ErrorMessage = "YearsOfExperience must be >= 0.")]
    public int YearsOfExperience { get; init; }
}

/// <summary>
/// DTO nhận dữ liệu từ client để cập nhật kỹ năng — PROFILE-01-02.
/// </summary>
public record SkillUpdateDto
{
    /// <summary>Tên kỹ năng — bắt buộc, tối đa 100 ký tự.</summary>
    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name must not exceed 100 characters.")]
    public string Name { get; init; } = "";

    /// <summary>Mức độ thành thạo từ 1 (Beginner) đến 5 (Expert).</summary>
    [Range(1, 5, ErrorMessage = "Proficiency must be between 1 and 5.")]
    public int Proficiency { get; init; }

    /// <summary>Số năm kinh nghiệm (>= 0).</summary>
    [Range(0, int.MaxValue, ErrorMessage = "YearsOfExperience must be >= 0.")]
    public int YearsOfExperience { get; init; }
}
