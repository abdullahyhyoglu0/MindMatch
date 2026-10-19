using MindMatch.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MindMatch.Domain.Entities
{
    public enum UserRole
    {
        User = 0,
        Moderator = 1,
        Admin = 2
    }



    public class User : BaseEntity
    {
        public string Email { get; private set; } = string.Empty;

        public string PasswordHash { get; private set; } = string.Empty;

        public bool IsActive { get; private set; } = true;

        public UserRole Role { get; private set; } = UserRole.User;

        public bool IsFrozenByAdmin { get; private set; }
        public Guid? FrozenByAdminId { get; private set; }
        public string? AdminFreezeReason { get; private set; }
        public DateTime? AdminFrozenUntilUtc { get; private set; }
        public DateTime? SelfDeactivatedAtUtc { get; private set; }
        public int SessionVersion { get; private set; }

        public User(string email, string passwordHash)
        {
            SetEmail(email);
            SetPasswordHash(passwordHash);
        }

        private User()
        {
            // ef icin metodyap
        }

        public void SetEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("E-posta boş olamaz.", nameof(email));

            Email = email.Trim().ToLowerInvariant();
            MarkAsUpdated();
        }

        public void SetPasswordHash(string passwordHash)
        {
            if (string.IsNullOrWhiteSpace(passwordHash))
                throw new ArgumentException(
                    "Parola özeti boş olamaz.",
                    nameof(passwordHash));

            PasswordHash = passwordHash;
            MarkAsUpdated();
        }

        public void Deactivate()
        {
            if (IsFrozenByAdmin)
                throw new InvalidOperationException("Yönetici tarafından dondurulan hesap kullanıcı tarafından aktifleştirilemez.");
            IsActive = false;
            SelfDeactivatedAtUtc = DateTime.UtcNow;
            SessionVersion++;
            MarkAsUpdated();
        }

        public void Activate()
        {
            if (IsFrozenByAdmin)
                throw new InvalidOperationException("Yönetici tarafından dondurulan hesap kullanıcı tarafından aktifleştirilemez.");
            IsActive = true;
            SelfDeactivatedAtUtc = null;
            SessionVersion++;
            MarkAsUpdated();
        }

        public void FreezeByAdmin(Guid adminId, string reason, DateTime? untilUtc = null)
        {
            if (adminId == Guid.Empty || string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Yönetici ve dondurma nedeni gereklidir.");
            if (untilUtc.HasValue && (untilUtc.Value.Kind != DateTimeKind.Utc || untilUtc.Value <= DateTime.UtcNow))
                throw new ArgumentException("Dondurma bitiş zamanı gelecekte ve UTC olmalıdır.", nameof(untilUtc));
            IsActive = false;
            IsFrozenByAdmin = true;
            FrozenByAdminId = adminId;
            AdminFreezeReason = reason.Trim();
            AdminFrozenUntilUtc = untilUtc;
            SessionVersion++;
            MarkAsUpdated();
        }

        public void ActivateByAdmin()
        {
            IsActive = true;
            IsFrozenByAdmin = false;
            FrozenByAdminId = null;
            AdminFreezeReason = null;
            AdminFrozenUntilUtc = null;
            SelfDeactivatedAtUtc = null;
            SessionVersion++;
            MarkAsUpdated();
        }

        public bool ExpireAdminFreeze(DateTime nowUtc)
        {
            nowUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
            if (!IsFrozenByAdmin || !AdminFrozenUntilUtc.HasValue || AdminFrozenUntilUtc.Value > nowUtc)
                return false;

            IsActive = true;
            IsFrozenByAdmin = false;
            FrozenByAdminId = null;
            AdminFreezeReason = null;
            AdminFrozenUntilUtc = null;
            SelfDeactivatedAtUtc = null;
            SessionVersion++;
            MarkAsUpdated();
            return true;
        }

        public void ChangeRole(UserRole role)
        {
            if (!Enum.IsDefined(role))
                throw new ArgumentException("Geçersiz kullanıcı rolü.", nameof(role));
            Role = role;
            SessionVersion++;
            MarkAsUpdated();
        }

        public void InvalidateSessions()
        {
            SessionVersion++;
            MarkAsUpdated();
        }
    }
}
