
using MindMatch.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace MindMatch.Application.Dtos.MoodCheckIn;

public class CreateMoodCheckInRequest
{
    [Required]
    [EnumDataType(typeof(MoodType))]
    public MoodType Mood { get; set; }

    [Required]
    [EnumDataType(typeof(EnergyLevel))]
    public EnergyLevel Energy { get; set; }

    [Required]
    [EnumDataType(typeof(ConversationType))]
    public ConversationType DesiredConversation { get; set; }
}
