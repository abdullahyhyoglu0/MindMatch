
using MindMatch.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace MindMatch.Application.Dtos.UserProfile;

public class UpdateProfileRequest
{
    [Required]
    [MaxLength(40)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? Nickname { get; set; }

    [MaxLength(300)]
    public string? Bio { get; set; }

    public DateOnly? BirthDate { get; set; }

    public Gender Gender { get; set; } = Gender.PreferNotToSay;

    public GenderPreference GenderPreference { get; set; }
        = GenderPreference.Everyone;

    [Range(18, 100)]
    public int? MinimumMatchAge { get; set; }

    [Range(18, 100)]
    public int? MaximumMatchAge { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }
}
