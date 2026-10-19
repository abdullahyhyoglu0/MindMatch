using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MindMatch.Application.Dtos.Match
{
    using global::MindMatch.Domain.Entities;



    public class MatchResponse
    {
        public Guid Id { get; set; }

        public Guid RequesterId { get; set; }

        public Guid ReceiverId { get; set; }

        public MatchStatus Status { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public DateTime? RespondedAtUtc { get; set; }

        public DateTime? EndedAtUtc { get; set; }
    }
}