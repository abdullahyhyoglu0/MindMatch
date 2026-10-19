using MindMatch.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MindMatch.Domain.Entities
{
  

    public enum MatchStatus
    {
        Pending = 1,
        Accepted = 2,
        Rejected = 3,
        Ended = 4,
        Expired = 5
    }

    public class Match : BaseEntity
    {
        public Guid RequesterId { get; private set; }

        public Guid ReceiverId { get; private set; }
        public string UserPairKey { get; private set; } = string.Empty;
        public int Version { get; private set; } = 1;

        public MatchStatus Status { get; private set; }

        public DateTime? RespondedAtUtc { get; private set; }

        public DateTime? EndedAtUtc { get; private set; }
        public Guid? ConversationId { get; private set; }

        public Conversation? Conversation { get; private set; }

        private Match()
        {
            // Entity Framework Core için.
        }

        public Match(Guid requesterId, Guid receiverId)
        {
            if (requesterId == Guid.Empty)
                throw new ArgumentException("İstek gönderen kullanıcı geçersiz.");

            if (receiverId == Guid.Empty)
                throw new ArgumentException("İsteği alan kullanıcı geçersiz.");

            if (requesterId == receiverId)
                throw new ArgumentException(
                    "Kullanıcı kendisine eşleşme isteği gönderemez.");

            RequesterId = requesterId;
            ReceiverId = receiverId;
            UserPairKey = BuildPairKey(requesterId, receiverId);
            Status = MatchStatus.Pending;
        }

        private static string BuildPairKey(Guid user1Id, Guid user2Id)
        {
            var first = user1Id.ToString("N");
            var second = user2Id.ToString("N");
            return StringComparer.Ordinal.Compare(first, second) < 0
                ? $"{first}:{second}"
                : $"{second}:{first}";
        }

        public void Accept(Guid respondingUserId)
        {
            EnsurePending();
            EnsureReceiver(respondingUserId);

            Status = MatchStatus.Accepted;
            RespondedAtUtc = DateTime.UtcNow;
            Version++;
            MarkAsUpdated();
        }

        public void Reject(Guid respondingUserId)
        {
            EnsurePending();
            EnsureReceiver(respondingUserId);

            Status = MatchStatus.Rejected;
            RespondedAtUtc = DateTime.UtcNow;
            Version++;
            MarkAsUpdated();
        }

        public void End(Guid requestingUserId)
        {
            if (Status != MatchStatus.Accepted)
                throw new InvalidOperationException(
                    "Yalnızca kabul edilmiş eşleşmeler sonlandırılabilir.");

            if (requestingUserId != RequesterId &&
                requestingUserId != ReceiverId)
            {
                throw new InvalidOperationException(
                    "Bu eşleşme üzerinde işlem yapma yetkiniz yok.");
            }

            Status = MatchStatus.Ended;
            EndedAtUtc = DateTime.UtcNow;
            Version++;
            MarkAsUpdated();
        }
        public void LinkConversation(Guid conversationId)
        {
            if (conversationId == Guid.Empty)
                throw new ArgumentException("Sohbet ID boş olamaz.");

            if (Status != MatchStatus.Accepted)
                throw new InvalidOperationException(
                    "Yalnızca kabul edilmiş eşleşmeler sohbete bağlanabilir.");

            if (ConversationId.HasValue &&
                ConversationId.Value != conversationId)
            {
                throw new InvalidOperationException(
                    "Bu eşleşme zaten başka bir sohbete bağlı.");
            }

            ConversationId = conversationId;
            Version++;
            MarkAsUpdated();
        }
        private void EnsurePending()
        {
            if (Status != MatchStatus.Pending)
                throw new InvalidOperationException(
                    "Bu eşleşme isteği artık yanıtlanamaz.");
        }

        private void EnsureReceiver(Guid respondingUserId)
        {
            if (respondingUserId != ReceiverId)
                throw new InvalidOperationException(
                    "Bu isteği yalnızca alıcı yanıtlayabilir.");
        }
    }
}
