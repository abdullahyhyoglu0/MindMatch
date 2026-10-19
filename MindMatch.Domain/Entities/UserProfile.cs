using MindMatch.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MindMatch.Domain.Entities
{
  
    public enum Gender
    {
        PreferNotToSay = 0,
        Male = 1,
        Female = 2,
        NonBinary = 3,
        Other = 4
    }

    public enum GenderPreference
    {
        Everyone = 0,
        Male = 1,
        Female = 2,
        NonBinary = 3
    }

    public class UserProfile : BaseEntity
    {
        public Guid UserId { get; private set; }

        public string DisplayName { get; private set; } = string.Empty;
        public string Nickname { get; private set; } = string.Empty;
        public string NormalizedNickname { get; private set; } = string.Empty;

        public string? Bio { get; private set; }

        public DateOnly? BirthDate { get; private set; }

        public Gender Gender { get; private set; } = Gender.PreferNotToSay;

        public GenderPreference GenderPreference { get; private set; }
            = GenderPreference.Everyone;

        public int? MinimumMatchAge { get; private set; }

        public int? MaximumMatchAge { get; private set; }

        public string? City { get; private set; }

        public bool IsProfileVisible { get; private set; }
        public bool AllowMessageRequests { get; private set; }
        public bool AllowCallsFromNonMatches { get; private set; }
        public bool ShowOnlineStatus { get; private set; } = true;
        public bool ShowLastSeen { get; private set; } = true;
        public bool ShowReadReceipts { get; private set; } = true;
        public bool ShowPremiumBadge { get; private set; } = true;
        public bool PremiumPriorityOptIn { get; private set; }

        private UserProfile()
        {
            // Entity Framework Core için.
        }

        public UserProfile(Guid userId, string displayName, string? nickname = null)
        {
            if (userId == Guid.Empty)
                throw new ArgumentException("Kullanıcı kimliği geçersiz.");

            UserId = userId;
            ChangeDisplayName(displayName);
            ChangeNickname(string.IsNullOrWhiteSpace(nickname) ? $"user{userId:N}"[..30] : nickname);
            IsProfileVisible = false;
        }

        public void ChangeNickname(string nickname)
        {
            if (string.IsNullOrWhiteSpace(nickname))
                throw new ArgumentException("Kullanıcı adı boş olamaz.");
            nickname = nickname.Trim();
            if (nickname.Length is < 3 or > 30 || nickname.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('_' or '.')))
                throw new ArgumentException("Kullanıcı adı 3-30 karakter olmalı; harf, rakam, nokta ve alt çizgi kullanılabilir.");
            Nickname = nickname;
            NormalizedNickname = nickname.ToUpperInvariant();
            MarkAsUpdated();
        }

        public void ChangeDisplayName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Görünen ad boş olamaz.");

            if (displayName.Trim().Length > 40)
                throw new ArgumentException(
                    "Görünen ad en fazla 40 karakter olabilir.");

            DisplayName = displayName.Trim();
            MarkAsUpdated();
        }

        public void UpdateBio(string? bio)
        {
            if (bio?.Length > 300)
                throw new ArgumentException(
                    "Biyografi en fazla 300 karakter olabilir.");

            Bio = string.IsNullOrWhiteSpace(bio) ? null : bio.Trim();
            MarkAsUpdated();
        }

        public void UpdateBirthDate(DateOnly? birthDate)
        {
            if (birthDate.HasValue &&
                birthDate.Value > DateOnly.FromDateTime(DateTime.UtcNow))
            {
                throw new ArgumentException(
                    "Doğum tarihi gelecekte olamaz.");
            }

            BirthDate = birthDate;
            MarkAsUpdated();
        }

        public void UpdateGender(Gender gender)
        {
            if (!Enum.IsDefined(gender))
                throw new ArgumentException("Geçersiz cinsiyet seçimi.");

            Gender = gender;
            MarkAsUpdated();
        }

        public void UpdateGenderPreference(GenderPreference preference)
        {
            if (!Enum.IsDefined(preference))
                throw new ArgumentException("Geçersiz eşleşme tercihi.");

            GenderPreference = preference;
            MarkAsUpdated();
        }

        public void UpdateMatchAgeRange(int? minimumAge, int? maximumAge)
        {
            if (minimumAge.HasValue && (minimumAge < 18 || minimumAge > 100))
                throw new ArgumentOutOfRangeException(nameof(minimumAge));

            if (maximumAge.HasValue && (maximumAge < 18 || maximumAge > 100))
                throw new ArgumentOutOfRangeException(nameof(maximumAge));

            if (minimumAge.HasValue &&
                maximumAge.HasValue &&
                minimumAge > maximumAge)
            {
                throw new ArgumentException(
                    "Minimum yaş, maksimum yaştan büyük olamaz.");
            }

            MinimumMatchAge = minimumAge;
            MaximumMatchAge = maximumAge;
            MarkAsUpdated();
        }

        public void UpdateCity(string? city)
        {
            if (city?.Trim().Length > 100)
                throw new ArgumentException(
                    "Şehir bilgisi en fazla 100 karakter olabilir.");

            City = string.IsNullOrWhiteSpace(city) ? null : city.Trim();
            MarkAsUpdated();
        }

        public void SetProfileVisibility(bool isVisible)
        {
            IsProfileVisible = isVisible;
            MarkAsUpdated();
        }

        public void UpdatePreferences(bool allowMessageRequests, bool allowCallsFromNonMatches,
            bool showOnlineStatus, bool showLastSeen, bool showReadReceipts, bool showPremiumBadge,
            bool premiumPriorityOptIn = false)
        {
            AllowMessageRequests = allowMessageRequests;
            AllowCallsFromNonMatches = allowCallsFromNonMatches;
            ShowOnlineStatus = showOnlineStatus;
            ShowLastSeen = showLastSeen;
            ShowReadReceipts = showReadReceipts;
            ShowPremiumBadge = showPremiumBadge;
            PremiumPriorityOptIn = premiumPriorityOptIn;
            MarkAsUpdated();
        }
    }

}
