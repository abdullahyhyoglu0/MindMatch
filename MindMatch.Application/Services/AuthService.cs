using MindMatch.Application.Dtos.Auth;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;
using System.Security.Cryptography;
using System.Text;


namespace MindMatch.Application.Services
{

    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IUserProfileRepository _profileRepository;
        private readonly IRefreshTokenRepository _refreshTokenRepository;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ITokenService _tokenService;
        private readonly IPasswordResetTokenRepository _passwordResetTokens;
        private readonly IPasswordResetEmailSender _passwordResetEmailSender;

        public AuthService(
            IUserRepository userRepository,
            IUserProfileRepository profileRepository,
            IRefreshTokenRepository refreshTokenRepository,
            IPasswordHasher passwordHasher,
            ITokenService tokenService,
            IPasswordResetTokenRepository passwordResetTokens,
            IPasswordResetEmailSender passwordResetEmailSender)
        {
            _userRepository = userRepository;
            _profileRepository = profileRepository;
            _refreshTokenRepository = refreshTokenRepository;
            _passwordHasher = passwordHasher;
            _tokenService = tokenService;
            _passwordResetTokens = passwordResetTokens;
            _passwordResetEmailSender = passwordResetEmailSender;
        }

        public async Task<AuthResponse> RegisterAsync(
            RegisterRequest request,
            CancellationToken cancellationToken = default)
        {
            var email = request.Email.Trim().ToLowerInvariant();

            if (await _userRepository.EmailExistsAsync(email, cancellationToken))
                throw new InvalidOperationException(
                    "Bu e-posta adresi zaten kayıtlı.");

            var passwordHash = _passwordHasher.HashPassword(request.Password);
            var user = new User(email, passwordHash);

            if (!string.IsNullOrWhiteSpace(request.Nickname) &&
                await _profileRepository.IsNicknameTakenAsync(request.Nickname.Trim().ToUpperInvariant(), null, cancellationToken))
                throw new InvalidOperationException("Bu kullanıcı adı zaten kullanılıyor.");

            await _userRepository.AddAsync(user, cancellationToken);

            var profile = new UserProfile(user.Id, request.DisplayName, request.Nickname);
            await _profileRepository.AddAsync(profile, cancellationToken);

            var response = await CreateTokenResponseAsync(user, cancellationToken);

            // Aynı scoped DbContext kullanılıyorsa kullanıcı, profil ve
            // refresh token değişikliklerini birlikte kaydeder.
            await _userRepository.SaveChangesAsync(cancellationToken);

            return response;
        }

        public async Task<AuthResponse> LoginAsync(
            LoginRequest request,
            CancellationToken cancellationToken = default)
        {
            var email = request.Email.Trim().ToLowerInvariant();

            var user = await _userRepository.GetByEmailAsync(
                email, cancellationToken);

            if (user is null ||
                !user.IsActive ||
                !_passwordHasher.VerifyPassword(
                    request.Password, user.PasswordHash))
            {
                throw new UnauthorizedAccessException(
                    "E-posta veya parola hatalı.");
            }

            var response = await CreateTokenResponseAsync(user, cancellationToken);
            await _userRepository.SaveChangesAsync(cancellationToken);

            return response;
        }

        public async Task<AuthResponse> ReactivateAsync(
            LoginRequest request,
            CancellationToken cancellationToken = default)
        {
            var email = request.Email.Trim().ToLowerInvariant();
            var user = await _userRepository.GetByEmailAsync(email, cancellationToken);
            if (user is null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
                throw new UnauthorizedAccessException("Kimlik bilgileri geçersiz.");
            if (user.IsFrozenByAdmin)
                throw new UnauthorizedAccessException("Bu hesap yönetici tarafından dondurulmuştur.");

            user.Activate();
            var response = await CreateTokenResponseAsync(user, cancellationToken);
            await _userRepository.SaveChangesAsync(cancellationToken);
            return response;
        }

        public async Task<AuthResponse> RefreshTokenAsync(
            string refreshToken,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                throw new UnauthorizedAccessException("Refresh token geçersiz.");

            var tokenHash = RefreshTokenHasher.Hash(refreshToken);

            var storedToken = await _refreshTokenRepository.GetByTokenHashAsync(
                tokenHash, cancellationToken);

            if (storedToken is null || !storedToken.IsActive)
                throw new UnauthorizedAccessException(
                    "Refresh token geçersiz, süresi dolmuş veya iptal edilmiş.");

            var user = await _userRepository.GetByIdAsync(
                storedToken.UserId, cancellationToken);

            if (user is null || !user.IsActive)
                throw new UnauthorizedAccessException("Kullanıcı aktif değil.");

            // Token rotasyonu: eski token iptal edilir, yenisi oluşturulur.
            storedToken.Revoke();

            var response = await CreateTokenResponseAsync(user, cancellationToken);

            await _refreshTokenRepository.SaveChangesAsync(cancellationToken);

            return response;
        }

        public async Task LogoutAsync(
            string refreshToken,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                return;

            var tokenHash = RefreshTokenHasher.Hash(refreshToken);

            var storedToken = await _refreshTokenRepository.GetByTokenHashAsync(
                tokenHash, cancellationToken);

            if (storedToken is null || storedToken.IsRevoked)
                return;

            storedToken.Revoke();

            await _refreshTokenRepository.SaveChangesAsync(cancellationToken);
        }

        public async Task LogoutAllAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty) throw new ArgumentException("Kullanıcı kimliği geçersiz.");
            await _refreshTokenRepository.RevokeAllForUserAsync(userId, cancellationToken);
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            user?.InvalidateSessions();
            await _userRepository.SaveChangesAsync(cancellationToken);
        }

        public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
                ?? throw new KeyNotFoundException("Kullanıcı bulunamadı.");
            if (!_passwordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash))
                throw new UnauthorizedAccessException("Mevcut parola hatalı.");
            if (string.Equals(request.CurrentPassword, request.NewPassword, StringComparison.Ordinal))
                throw new ArgumentException("Yeni parola mevcut paroladan farklı olmalıdır.");
            user.SetPasswordHash(_passwordHasher.HashPassword(request.NewPassword));
            user.InvalidateSessions();
            await _refreshTokenRepository.RevokeAllForUserAsync(userId, cancellationToken);
            await _userRepository.SaveChangesAsync(cancellationToken);
        }

        public async Task<UserMeResponse> GetMeAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
                ?? throw new KeyNotFoundException("Kullanıcı bulunamadı.");
            return new UserMeResponse
            {
                UserId = user.Id, Email = user.Email, IsActive = user.IsActive,
                Role = user.Role.ToString(), CreatedAtUtc = user.CreatedAtUtc
            };
        }

        public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (string.IsNullOrWhiteSpace(request.Email)) return;
            var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
            if (user is null || user.IsFrozenByAdmin) return;

            var tokenBytes = RandomNumberGenerator.GetBytes(32);
            var rawToken = Convert.ToBase64String(tokenBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
            var now = DateTime.UtcNow;
            await _passwordResetTokens.RevokeActiveForUserAsync(user.Id, cancellationToken);
            await _passwordResetTokens.AddAsync(new PasswordResetToken(user.Id, hash, now.AddMinutes(30)), cancellationToken);
            await _userRepository.SaveChangesAsync(cancellationToken);
            await _passwordResetEmailSender.SendAsync(user.Email, rawToken, cancellationToken);
        }

        public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (string.IsNullOrWhiteSpace(request.Token)) throw new UnauthorizedAccessException("Token geçersiz veya süresi dolmuş.");
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Token)));
            var token = await _passwordResetTokens.GetByHashAsync(hash, cancellationToken);
            var now = DateTime.UtcNow;
            if (token is null || !token.IsActive(now)) throw new UnauthorizedAccessException("Token geçersiz veya süresi dolmuş.");
            var user = await _userRepository.GetByIdAsync(token.UserId, cancellationToken)
                ?? throw new UnauthorizedAccessException("Token geçersiz veya süresi dolmuş.");
            if (user.IsFrozenByAdmin) throw new UnauthorizedAccessException("Bu hesap yönetici tarafından dondurulmuştur.");
            token.MarkUsed(now);
            user.SetPasswordHash(_passwordHasher.HashPassword(request.NewPassword));
            user.InvalidateSessions();
            await _refreshTokenRepository.RevokeAllForUserAsync(user.Id, cancellationToken);
            await _userRepository.SaveChangesAsync(cancellationToken);
        }

        private async Task<AuthResponse> CreateTokenResponseAsync(
            User user,
            CancellationToken cancellationToken)
        {
            var rawRefreshToken = _tokenService.GenerateRefreshToken();
            var refreshExpiry = _tokenService.GetRefreshTokenExpiryUtc();

            var refreshTokenEntity = new RefreshToken(
                user.Id,
                RefreshTokenHasher.Hash(rawRefreshToken),
                refreshExpiry);

            await _refreshTokenRepository.AddAsync(
                refreshTokenEntity, cancellationToken);

            return new AuthResponse
            {
                UserId = user.Id,
                Email = user.Email,
                AccessToken = _tokenService.GenerateAccessToken(
                    user.Id, user.Email, user.Role, user.SessionVersion),
                AccessTokenExpiresAtUtc = _tokenService.GetAccessTokenExpiryUtc(),
                RefreshToken = rawRefreshToken,
                RefreshTokenExpiresAtUtc = refreshExpiry,
                RequiresEmailVerification = false
            };
        }
    }
}
