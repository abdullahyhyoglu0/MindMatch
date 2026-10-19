using MindMatch.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MindMatch.Domain.Entities
{


    public enum MoodType
    {
        Happy = 1,
        Sad = 2,
        Stressed = 3,
        Lonely = 4,
        Calm = 5,
        Anxious = 6,
        Excited = 7,
        Tired = 8,
        Neutral = 9
    }

    public enum EnergyLevel
    {
        Low = 1,
        Medium = 2,
        High = 3
    }

    public enum ConversationType
    {
        Venting = 1,
        Advice = 2,
        Fun = 3,
        DeepTalk = 4,
        Casual = 5,
        NewFriendship = 6,
        Listening = 7
    }

    public class MoodCheckIn : BaseEntity
    {
        public Guid UserId { get; private set; }

        public MoodType Mood { get; private set; }

        public EnergyLevel Energy { get; private set; }

        public ConversationType DesiredConversation { get; private set; }

        public DateTime ExpiresAtUtc { get; private set; }

        private MoodCheckIn()
        {
            // Entity Framework Core için.
        }

        public MoodCheckIn(
            Guid userId,
            MoodType mood,
            EnergyLevel energy,
            ConversationType desiredConversation)
        {
            if (userId == Guid.Empty)
                throw new ArgumentException("Kullanıcı kimliği geçersiz.");

            if (!Enum.IsDefined(mood))
                throw new ArgumentException("Geçersiz ruh hâli.");

            if (!Enum.IsDefined(energy))
                throw new ArgumentException("Geçersiz enerji seviyesi.");

            if (!Enum.IsDefined(desiredConversation))
                throw new ArgumentException("Geçersiz sohbet türü.");

            UserId = userId;
            Mood = mood;
            Energy = energy;
            DesiredConversation = desiredConversation;
            ExpiresAtUtc = DateTime.UtcNow.AddHours(2);
        }

        public bool IsExpired()
        {
            return DateTime.UtcNow >= ExpiresAtUtc;
        }
    }

}
