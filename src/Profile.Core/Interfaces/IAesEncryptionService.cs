namespace Profile.Core.Interfaces;

/// <summary>
/// Dịch vụ mã hóa dữ liệu nhạy cảm tầng ứng dụng — SEC-08.
/// Sử dụng AES-256-GCM với Nonce/IV ngẫu nhiên 12 bytes mỗi lần mã hóa.
/// Format lưu trữ: Base64(Nonce[12] + Tag[16] + Ciphertext).
/// </summary>
public interface IAesEncryptionService
{
    /// <summary>
    /// Mã hóa chuỗi plaintext. Trả về null nếu input null.
    /// </summary>
    string? Encrypt(string? plainText);

    /// <summary>
    /// Giải mã chuỗi ciphertext Base64. Trả về null nếu input null.
    /// </summary>
    string? Decrypt(string? cipherText);
}
