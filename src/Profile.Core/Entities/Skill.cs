using SharedKernel;

namespace Profile.Core.Entities;

/// <summary>
/// Kỹ năng của ứng viên — PROFILE-01-02 (SRS 3.6.4).
/// Proficiency phải nằm trong khoảng 1-5 (check constraint ở DB).
/// </summary>
public class Skill : Entity
{
    /// <summary>FK đến UserProfile.</summary>
    public Guid ProfileId { get; private set; }

    /// <summary>Navigation ngược lên profile.</summary>
    public UserProfile? Profile { get; private set; }

    /// <summary>Tên kỹ năng — max 100, required.</summary>
    public string Name { get; private set; } = "";

    /// <summary>Mức độ thành thạo từ 1 (Beginner) đến 5 (Expert).</summary>
    public int Proficiency { get; private set; }

    /// <summary>Số năm kinh nghiệm với kỹ năng này (>= 0).</summary>
    public int YearsOfExperience { get; private set; }

    // EF Core constructor
    private Skill() { }

    /// <summary>Thêm kỹ năng mới vào profile.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Proficiency không hợp lệ (ngoài 1-5).</exception>
    public Skill(Guid profileId, string name, int proficiency, int yearsOfExperience)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ValidateProficiency(proficiency);
        if (yearsOfExperience < 0)
            throw new ArgumentOutOfRangeException(nameof(yearsOfExperience), "Years of experience must be >= 0.");

        ProfileId = profileId;
        Name = name.Trim();
        Proficiency = proficiency;
        YearsOfExperience = yearsOfExperience;
    }

    /// <summary>Cập nhật thông tin kỹ năng.</summary>
    public void Update(string name, int proficiency, int yearsOfExperience)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ValidateProficiency(proficiency);
        if (yearsOfExperience < 0)
            throw new ArgumentOutOfRangeException(nameof(yearsOfExperience), "Years of experience must be >= 0.");

        Name = name.Trim();
        Proficiency = proficiency;
        YearsOfExperience = yearsOfExperience;
        Touch();
    }

    private static void ValidateProficiency(int proficiency)
    {
        if (proficiency is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(proficiency), "Proficiency must be between 1 and 5.");
    }
}
