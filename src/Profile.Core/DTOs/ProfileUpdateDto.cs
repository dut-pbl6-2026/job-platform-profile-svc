using System.ComponentModel.DataAnnotations;

namespace Profile.Core.DTOs;

/// <summary>
/// DTO nhận dữ liệu từ client để tạo mới hoặc cập nhật hồ sơ — PROFILE-01-01.
/// Các trường PII (Phone, Address, DateOfBirth) được mã hóa ở tầng API trước khi lưu (SEC-08).
/// </summary>
public record ProfileUpdateDto
{
    /// <summary>Họ tên đầy đủ — bắt buộc, tối đa 128 ký tự.</summary>
    [Required(ErrorMessage = "FullName is required.")]
    [MaxLength(128, ErrorMessage = "FullName must not exceed 128 characters.")]
    public string FullName { get; init; } = "";

    /// <summary>Tiêu đề nghề nghiệp — tùy chọn, tối đa 256 ký tự.</summary>
    [MaxLength(256, ErrorMessage = "Headline must not exceed 256 characters.")]
    public string? Headline { get; init; }

    /// <summary>Tóm tắt bản thân — tùy chọn, tối đa 2000 ký tự.</summary>
    [MaxLength(2000, ErrorMessage = "Summary must not exceed 2000 characters.")]
    public string? Summary { get; init; }

    /// <summary>URL ảnh đại diện — tùy chọn, tối đa 512 ký tự.</summary>
    [MaxLength(512, ErrorMessage = "AvatarUrl must not exceed 512 characters.")]
    public string? AvatarUrl { get; init; }

    /// <summary>Số điện thoại plaintext — sẽ được mã hóa AES-256-GCM (SEC-08) trước khi lưu.</summary>
    public string? Phone { get; init; }

    /// <summary>Địa chỉ plaintext — sẽ được mã hóa AES-256-GCM (SEC-08) trước khi lưu.</summary>
    public string? Address { get; init; }

    /// <summary>Ngày sinh — sẽ được mã hóa AES-256-GCM (SEC-08) trước khi lưu.</summary>
    public DateOnly? DateOfBirth { get; init; }
}
