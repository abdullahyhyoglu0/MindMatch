using System.ComponentModel.DataAnnotations;
using MindMatch.Domain.Entities;

namespace MindMatch.Application.Dtos.Stories;

public sealed class CreateStoryRequest
{
    public Guid MediaAssetId { get; set; }
    public StoryMediaType MediaType { get; set; }
    [MaxLength(500)] public string? Caption { get; set; }
}
