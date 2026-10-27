
using MindMatch.Application.Dtos.UserProfile;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;

namespace MindMatch.Application.Services;

public class UserProfileService : IUserProfileService
{
    private readonly IUserProfileRepository _profileRepository;
    private readonly IUserRepository _users;
    private readonly IUserInterestRepository _userInterestRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserBlockRepository _userBlockRepository;
    private readonly IUserFollowRepository _follows;
    private readonly IConversationRepository _conversations;
    private readonly IPremiumService _premiumService;

    public UserProfileService(
        IUserProfileRepository profileRepository,
        IUserRepository users,
        IUserInterestRepository userInterestRepository,
        IUserBlockRepository userBlockRepository,
        IUserFollowRepository follows,
        IConversationRepository conversations,
        IPremiumService premiumService,
        IUnitOfWork unitOfWork)
    {
        _profileRepository = profileRepository;
        _users = users;
        _userInterestRepository = userInterestRepository;
        _userBlockRepository = userBlockRepository;
        _follows = follows;
        _conversations = conversations;
        _premiumService = premiumService;
        _unitOfWork = unitOfWork;
    }

    public async Task<UserProfileResponse> GetMyProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);

        var profile = await GetRequiredProfileAsync(
            userId, cancellationToken);

        var response = MapToResponse(profile);

        response.InterestIds =
            await _userInterestRepository.GetInterestIdsByUserIdAsync(
                userId, cancellationToken);

        return response;
    }

    public async Task<UserProfileResponse> UpdateMyProfileAsync(
        Guid userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        ArgumentNullException.ThrowIfNull(request);

        var profile = await GetRequiredProfileAsync(
            userId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.Nickname) &&
            await _profileRepository.IsNicknameTakenAsync(request.Nickname.Trim().ToUpperInvariant(), userId, cancellationToken))
            throw new InvalidOperationException("Bu kullanıcı adı zaten kullanılıyor.");

        profile.ChangeDisplayName(request.DisplayName);
        if (!string.IsNullOrWhiteSpace(request.Nickname))
            profile.ChangeNickname(request.Nickname);
        profile.UpdateBio(request.Bio);
        profile.UpdateBirthDate(request.BirthDate);
        profile.UpdateGender(request.Gender);
        profile.UpdateGenderPreference(request.GenderPreference);

        profile.UpdateMatchAgeRange(
            request.MinimumMatchAge,
            request.MaximumMatchAge);

        profile.UpdateCity(request.City);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var response = MapToResponse(profile);

        response.InterestIds =
            await _userInterestRepository.GetInterestIdsByUserIdAsync(
                userId, cancellationToken);

        return response;
    }

    public async Task SetProfileVisibilityAsync(
        Guid userId,
        bool isVisible,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);

        var profile = await GetRequiredProfileAsync(
            userId, cancellationToken);

        profile.SetProfileVisibility(isVisible);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<UserProfileResponse> GetPublicProfileAsync(
        Guid viewerId, Guid targetUserId, CancellationToken cancellationToken = default)
    {
        ValidateUserId(viewerId);
        ValidateUserId(targetUserId);
        if (viewerId == targetUserId)
            return await GetMyProfileAsync(viewerId, cancellationToken);
        if (await _userBlockRepository.IsBlockedEitherWayAsync(viewerId, targetUserId, cancellationToken))
            throw new KeyNotFoundException("Profil bulunamadı.");

        var targetUser = await _users.GetByIdAsync(targetUserId, cancellationToken);
        if (targetUser is null || !targetUser.IsActive)
            throw new KeyNotFoundException("Profil bulunamadı.");

        var profile = await _profileRepository.GetByUserIdAsync(targetUserId, cancellationToken);
        if (profile is null)
            throw new KeyNotFoundException("Profil bulunamadı.");

        var canView = await SocialAccess.CanViewProfileAsync(viewerId, profile, _follows, cancellationToken);
        var response = MapToResponse(profile);
        await EnrichSocialAsync(response, viewerId, profile, canView, cancellationToken);
        response.Age = CalculateAge(profile.BirthDate);
        ProfilePrivacyMapper.ApplyPublicView(response);
        response.CanViewProfile = canView;
        response.IsProfileVisible = profile.IsProfileVisible;
        if (!canView)
        {
            response.Bio = null;
            response.City = null;
            response.Gender = Gender.PreferNotToSay;
            response.GenderPreference = GenderPreference.Everyone;
            response.MinimumMatchAge = null;
            response.MaximumMatchAge = null;
            response.Age = null;
            response.InterestIds = [];
            response.ShowPremiumBadge = false;
            return response;
        }

        response.ShowPremiumBadge = profile.ShowPremiumBadge && await _premiumService.IsPremiumAsync(targetUserId, cancellationToken);
        response.InterestIds = await _userInterestRepository.GetInterestIdsByUserIdAsync(targetUserId, cancellationToken);
        return response;
    }

    public async Task UpdatePreferencesAsync(
        Guid userId, ProfilePreferencesRequest request, CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        ArgumentNullException.ThrowIfNull(request);
        var profile = await GetRequiredProfileAsync(userId, cancellationToken);
        profile.UpdatePreferences(request.AllowMessageRequests, request.AllowCallsFromNonMatches,
            request.ShowOnlineStatus, request.ShowLastSeen, request.ShowReadReceipts, request.ShowPremiumBadge,
            request.PremiumPriorityOptIn, request.ShowFollowers, request.ShowFollowing);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateInterestsAsync(Guid userId, IReadOnlyCollection<Guid> interestIds,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        ArgumentNullException.ThrowIfNull(interestIds);
        if (interestIds.Count > 50 || interestIds.Any(x => x == Guid.Empty))
            throw new ArgumentException("En fazla 50 geçerli ilgi alanı seçebilirsiniz.");
        await GetRequiredProfileAsync(userId, cancellationToken);
        if (!await _userInterestRepository.ReplaceForUserAsync(userId, interestIds, cancellationToken))
            throw new KeyNotFoundException("İlgi alanlarından biri bulunamadı.");
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<UserProfile> GetRequiredProfileAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var profile = await _profileRepository.GetByUserIdAsync(
            userId, cancellationToken);

        return profile ?? throw new InvalidOperationException(
            "Kullanıcı profili bulunamadı.");
    }

    private static UserProfileResponse MapToResponse(
        UserProfile profile)
    {
        return new UserProfileResponse
        {
            UserId = profile.UserId,
            DisplayName = profile.DisplayName,
            Nickname = profile.Nickname,
            Bio = profile.Bio,
            BirthDate = profile.BirthDate,
            Gender = profile.Gender,
            GenderPreference = profile.GenderPreference,
            MinimumMatchAge = profile.MinimumMatchAge,
            MaximumMatchAge = profile.MaximumMatchAge,
            City = profile.City,
            IsProfileVisible = profile.IsProfileVisible,
            AllowMessageRequests = profile.AllowMessageRequests,
            AllowCallsFromNonMatches = profile.AllowCallsFromNonMatches,
            ShowOnlineStatus = profile.ShowOnlineStatus,
            ShowLastSeen = profile.ShowLastSeen,
            ShowReadReceipts = profile.ShowReadReceipts,
            ShowPremiumBadge = profile.ShowPremiumBadge,
            PremiumPriorityOptIn = profile.PremiumPriorityOptIn
        };
    }

    private async Task EnrichSocialAsync(UserProfileResponse response, Guid viewerId, UserProfile profile,
        bool canView, CancellationToken cancellationToken)
    {
        if (viewerId == profile.UserId) response.FollowRelation = FollowRelation.None;
        else
        {
            var pair = await _follows.GetPairAsync(viewerId, profile.UserId, cancellationToken);
            response.FollowRelation = UserFollow.RelationOf(pair.Outgoing, pair.Incoming);
        }
        response.FollowerCount = await _follows.CountAcceptedFollowersAsync(profile.UserId, cancellationToken);
        response.FollowingCount = await _follows.CountAcceptedFollowingAsync(profile.UserId, cancellationToken);
        response.MessagingMode = SocialAccess.ResolveMessaging(profile, canView, false);
        response.ShowFollowers = profile.ShowFollowers;
        response.ShowFollowing = profile.ShowFollowing;
    }

    private static int? CalculateAge(DateOnly? birthDate)
    {
        if (!birthDate.HasValue) return null;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var age = today.Year - birthDate.Value.Year;
        if (birthDate.Value > today.AddYears(-age)) age--;
        return age;
    }

    private static void ValidateUserId(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "Kullanıcı kimliği geçersiz.",
                nameof(userId));
        }
    }
}
