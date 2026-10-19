using MindMatch.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MindMatch.Application.Dtos.MoodCheckIn
{


    public class MoodCheckInResponse
    {
        public Guid Id { get; set; }

        public MoodType Mood { get; set; }

        public EnergyLevel Energy { get; set; }

        public ConversationType DesiredConversation { get; set; }

        public DateTime ExpiresAtUtc { get; set; }

        public bool IsExpired { get; set; }
    }

}