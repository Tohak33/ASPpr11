using System.ComponentModel.DataAnnotations;

namespace para.DTOs;

public sealed class LoginDTO
{
    [Required, StringLength(50)]
    public string Login { get; set; } = "";
    [Required, StringLength(100)]
    public string Password { get; set; } = "";
}

public sealed class RegisterDTO
{
    [Required, StringLength(50, MinimumLength = 3)]
    public string Login { get; set; } = "";
    [Required, StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = "";
    [Required, StringLength(100)]
    public string FullName { get; set; } = "";
}

public sealed record AccessTokenDTO(string AccessToken, string TokenType, DateTime ExpiresAt, UserProfileDTO User);
