using SharedKernel;

namespace Profile.Core.Entities;

/// <summary>
/// Kinh nghiệm làm việc của ứng viên — PROFILE-01-03 (SRS 3.6.4).
/// Cho phép nhiều mục kinh nghiệm trên mỗi hồ sơ.
/// </summary>
public class WorkExperience : Entity
{
    /// <summary>FK đến UserProfile.</summary>
    public Guid ProfileId { get; private set; }

    /// <summary>Navigation ngược lên profile.</summary>
    public UserProfile? Profile { get; private set; }

    /// <summary>Tên công ty — max 256, required.</summary>
    public string Company { get; private set; } = "";

    /// <summary>Chức danh — max 128, required.</summary>
    public string Title { get; private set; } = "";

    /// <summary>Ngày bắt đầu làm việc.</summary>
    public DateOnly StartDate { get; private set; }

    /// <summary>Ngày kết thúc — null nếu IsCurrent = true.</summary>
    public DateOnly? EndDate { get; private set; }

    /// <summary>Có đang làm việc tại đây không.</summary>
    public bool IsCurrent { get; private set; }

    /// <summary>Mô tả công việc — max 2000, nullable.</summary>
    public string? Description { get; private set; }

    // EF Core constructor
    private WorkExperience() { }

    /// <summary>Thêm mục kinh nghiệm làm việc mới.</summary>
    public WorkExperience(
        Guid profileId,
        string company,
        string title,
        DateOnly startDate,
        DateOnly? endDate,
        bool isCurrent,
        string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(company, nameof(company));
        ArgumentException.ThrowIfNullOrWhiteSpace(title, nameof(title));
        ValidateDates(startDate, endDate, isCurrent);

        ProfileId = profileId;
        Company = company.Trim();
        Title = title.Trim();
        StartDate = startDate;
        EndDate = endDate;
        IsCurrent = isCurrent;
        Description = description?.Trim();
    }

    /// <summary>Cập nhật thông tin kinh nghiệm.</summary>
    public void Update(
        string company,
        string title,
        DateOnly startDate,
        DateOnly? endDate,
        bool isCurrent,
        string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(company, nameof(company));
        ArgumentException.ThrowIfNullOrWhiteSpace(title, nameof(title));
        ValidateDates(startDate, endDate, isCurrent);

        Company = company.Trim();
        Title = title.Trim();
        StartDate = startDate;
        EndDate = endDate;
        IsCurrent = isCurrent;
        Description = description?.Trim();
        Touch();
    }

    private static void ValidateDates(DateOnly startDate, DateOnly? endDate, bool isCurrent)
    {
        if (!isCurrent && endDate.HasValue && endDate.Value < startDate)
            throw new ArgumentException("EndDate must be >= StartDate when not current.", nameof(endDate));
    }
}
