using System.ComponentModel.DataAnnotations;

namespace MindMatch.Application.Dtos.Match;

public sealed class SendMatchRequest
{
    [Required] public Guid ReceiverId { get; set; }
}
