namespace Profile.Core.Validation;

/// <summary>
/// Boundary validation for the profile avatar URL. The URL is rendered by clients, so only
/// absolute https URLs are accepted: javascript:, data:, http: and every other scheme are
/// rejected, as are URLs without a host or with user information.
/// </summary>
public static class AvatarUrlPolicy
{
    public const int MaxLength = 512;

    /// <summary>
    /// Validates and normalizes an avatar URL. Null, empty or whitespace-only input clears the avatar
    /// and is accepted as null.
    /// </summary>
    public static bool TryNormalize(string? raw, out string? normalized, out string? error)
    {
        normalized = null;
        error = null;

        var value = raw?.Trim();
        if (string.IsNullOrEmpty(value))
            return true;

        if (value.Length > MaxLength)
        {
            error = $"AvatarUrl must not exceed {MaxLength} characters.";
            return false;
        }

        if (value.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
        {
            error = "AvatarUrl must not contain whitespace or control characters.";
            return false;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            error = "AvatarUrl must be an absolute https URL.";
            return false;
        }

        if (string.IsNullOrEmpty(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo))
        {
            error = "AvatarUrl must have a host and must not contain user information.";
            return false;
        }

        normalized = value;
        return true;
    }
}
