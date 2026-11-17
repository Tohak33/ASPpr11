using System.ComponentModel.DataAnnotations;

namespace para.DTOs;

public sealed class UserProfileDTO
{
    public int Id { get; set; }
    public string Login { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Role { get; set; } = "";
}

public sealed class UpdateProfileDTO
{
    [Required, StringLength(100)]
    public string FullName { get; set; } = "";
}
