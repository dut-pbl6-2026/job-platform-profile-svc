using SharedKernel;

namespace Profile.Core.Entities;

/// <summary>
/// Hồ sơ người dùng — PROFILE-01-01 (SRS 3.6.4).
/// Chứa thông tin cá nhân. Trường PII (phone, address, dateOfBirth)
/// được lưu dạng ciphertext AES-256-GCM ở tầng ứng dụng (SEC-08).
/// </summary>
public class UserProfile : Entity
{
    /// <summary>UserId liên kết 1:1 với User trong Auth Service.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Họ tên đầy đủ — max 128, required.</summary>
    public string FullName { get; private set; } = "";

    /// <summary>Tiêu đề nghề nghiệp — max 256, nullable.</summary>
    public string? Headline { get; private set; }

    /// <summary>Tóm tắt bản thân — max 2000, nullable.</summary>
    public string? Summary { get; private set; }

    /// <summary>URL ảnh đại diện — max 512, nullable.</summary>
    public string? AvatarUrl { get; private set; }

    /// <summary>Số điện thoại đã mã hóa AES-256-GCM (SEC-08). Không bao giờ lưu plaintext.</summary>
    public string? PhoneEncrypted { get; private set; }

    /// <summary>Địa chỉ đã mã hóa AES-256-GCM (SEC-08). Không bao giờ lưu plaintext.</summary>
    public string? AddressEncrypted { get; private set; }

    /// <summary>Ngày sinh đã mã hóa AES-256-GCM (SEC-08). Không bao giờ lưu plaintext.</summary>
    public string? DateOfBirthEncrypted { get; private set; }

    /// <summary>Danh sách kỹ năng liên kết với hồ sơ.</summary>
    public ICollection<Skill> Skills { get; private set; } = new List<Skill>();

    /// <summary>Danh sách kinh nghiệm làm việc liên kết với hồ sơ.</summary>
    public ICollection<WorkExperience> Experiences { get; private set; } = new List<WorkExperience>();

    /// <summary>Danh sách lịch sử giáo dục liên kết với hồ sơ.</summary>
    public ICollection<Education> Educations { get; private set; } = new List<Education>();

    // EF Core constructor
    private UserProfile() { }

    /// <summary>Tạo hồ sơ mới cho user.</summary>
    public UserProfile(Guid userId, string fullName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName, nameof(fullName));
        UserId = userId;
        FullName = fullName.Trim();
    }

    /// <summary>
    /// Cập nhật thông tin hồ sơ.
    /// Các trường PII đã được mã hóa bởi caller trước khi truyền vào (SEC-08).
    /// </summary>
    public void Update(
        string fullName,
        string? headline,
        string? summary,
        string? avatarUrl,
        string? phoneEncrypted,
        string? addressEncrypted,
        string? dateOfBirthEncrypted)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName, nameof(fullName));
        FullName = fullName.Trim();
        Headline = headline?.Trim();
        Summary = summary?.Trim();
        AvatarUrl = avatarUrl?.Trim();
        PhoneEncrypted = phoneEncrypted;
        AddressEncrypted = addressEncrypted;
        DateOfBirthEncrypted = dateOfBirthEncrypted;
        Touch();
    }
}
