using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using System.Text;
using crm_api.DTOs;
using crm_api.Models;
using crm_api.Interfaces;
using crm_api.UnitOfWork;
using crm_api.Hubs;
using Hangfire;
using Infrastructure.BackgroundJobs.Interfaces;
using Microsoft.Extensions.Configuration;

namespace crm_api.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IJwtService _jwtService;
        private readonly ILocalizationService _localizationService;
        private readonly IHubContext<AuthHub> _hubContext;
        private readonly IConfiguration _configuration;
        private readonly IUserService _userService;
        private readonly IUserSessionCacheService _userSessionCacheService;
        private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);
        public AuthService(
            IUnitOfWork unitOfWork,
            IJwtService jwtService,
            ILocalizationService localizationService,
            IHubContext<AuthHub> hubContext,
            IConfiguration configuration,
            IUserService userService,
            IUserSessionCacheService userSessionCacheService)
        {
            _unitOfWork = unitOfWork;
            _jwtService = jwtService;
            _localizationService = localizationService;
            _hubContext = hubContext;
            _configuration = configuration;
            _userService = userService;
            _userSessionCacheService = userSessionCacheService;
        }

        public async Task<ApiResponse<UserDto>> GetUserByUsernameAsync(string username)
        {
            try
            {
                var query = _unitOfWork.Users.Query().Include(u => u.RoleNavigation);
                var user = await query.FirstOrDefaultAsync(u => u.Username == username).ConfigureAwait(false);
                
                if (user == null)
                {
                    var nf = _localizationService.GetLocalizedString("AuthUserNotFound");
                    return ApiResponse<UserDto>.ErrorResult(nf, nf, 404);
                }

                var dto = MapToUserDto(user);
                return ApiResponse<UserDto>.SuccessResult(dto, _localizationService.GetLocalizedString("AuthUserRetrievedSuccessfully"));
            }
            catch (Exception ex)
            {
                return ApiResponse<UserDto>.ErrorResult(_localizationService.GetLocalizedString("AuthErrorOccurred"), ex.Message ?? string.Empty, 500);
            }
        }

        public async Task<ApiResponse<UserDto>> GetUserByIdAsync(long id)
        {
            try
            {
                var user = await _unitOfWork.Users.Query().Include(u => u.RoleNavigation).FirstOrDefaultAsync(u => u.Id == id).ConfigureAwait(false);
                
                if (user == null)
                {
                    var nf = _localizationService.GetLocalizedString("AuthUserNotFound");
                    return ApiResponse<UserDto>.ErrorResult(nf, nf, 404);
                }

                var dto = MapToUserDto(user);
                return ApiResponse<UserDto>.SuccessResult(dto, _localizationService.GetLocalizedString("AuthUserRetrievedSuccessfully"));
            }
            catch (Exception ex)
            {
                return ApiResponse<UserDto>.ErrorResult(_localizationService.GetLocalizedString("AuthErrorOccurred"), ex.Message ?? string.Empty, 500);
            }
        }

        public async Task<ApiResponse<UserDto>> RegisterUserAsync(RegisterDto registerDto)
        {
            try
            {
                // Check if user already exists
                var existingUserResponse = await GetUserByUsernameAsync(registerDto.Username).ConfigureAwait(false);
                if (existingUserResponse.Success)
                {
                    var msg = _localizationService.GetLocalizedString("AuthUserAlreadyExists");
                    return ApiResponse<UserDto>.ErrorResult(msg, msg, 400);
                }

                // Create new user
                var user = new User
                {
                    Username = registerDto.Username,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password),
                    Email = registerDto.Email,
                    FirstName = registerDto.FirstName,
                    LastName = registerDto.LastName
                };

                await _unitOfWork.Users.AddAsync(user).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                var dto = MapToUserDto(user);
                return ApiResponse<UserDto>.SuccessResult(dto, _localizationService.GetLocalizedString("AuthUserRegisteredSuccessfully"));
            }
            catch (Exception ex)
            {
                return ApiResponse<UserDto>.ErrorResult(_localizationService.GetLocalizedString("AuthRegistrationFailed"), ex.Message ?? string.Empty, 500);
            }
        }

        public async Task<ApiResponse<string>> LoginAsync(LoginRequest request)
        {
            try
            {
                var loginDto = new LoginDto
                {
                    Username = request.Email,
                    Password = request.Password
                };
                // Email veya username ile kullanıcı arama
                var user = await _unitOfWork.Users.Query().FirstOrDefaultAsync(u => u.Username == loginDto.Username || u.Email == loginDto.Username).ConfigureAwait(false);
                
                if (user == null)
                {
                    var msg = _localizationService.GetLocalizedString("Error.User.InvalidCredentials");
                    return ApiResponse<string>.ErrorResult(msg, msg, 401);
                }

                if (!user.IsActive)
                {
                    var msg = _localizationService.GetLocalizedString("Error.User.AccountInactive");
                    return ApiResponse<string>.ErrorResult(msg, msg, 401);
                }
                
                if (!BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
                {
                    var msg = _localizationService.GetLocalizedString("Error.User.InvalidCredentials");
                    return ApiResponse<string>.ErrorResult(msg, msg, 401);
                }

                var activeSession = await _unitOfWork.UserSessions.Query(tracking: true)
                    .Where(s => s.UserId == user.Id && s.RevokedAt == null)
                    .OrderByDescending(s => s.CreatedAt)
                    .FirstOrDefaultAsync()
                    .ConfigureAwait(false);
                if (activeSession != null)
                {
                    if (IsSessionTokenReusable(activeSession))
                    {
                        var hadRefreshToken = !string.IsNullOrWhiteSpace(user.RefreshToken) &&
                            user.RefreshTokenExpiryTime.HasValue &&
                            user.RefreshTokenExpiryTime.Value > DateTime.UtcNow;
                        EnsureRefreshToken(user);
                        if (!hadRefreshToken)
                        {
                            await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                        }

                        var existingTokenResult = _jwtService.GenerateToken(user, activeSession.SessionId, activeSession.CreatedAt);
                        if (!existingTokenResult.Success || string.IsNullOrWhiteSpace(existingTokenResult.Data))
                        {
                            return ApiResponse<string>.ErrorResult(
                                _localizationService.GetLocalizedString("Error.User.LoginFailed"),
                                existingTokenResult.ExceptionMessage ?? existingTokenResult.Message ?? string.Empty,
                                500);
                        }

                        _userSessionCacheService.SetActiveSession(
                            activeSession.SessionId,
                            activeSession.UserId,
                            GetSessionExpiryUtc(activeSession.CreatedAt));

                        return ApiResponse<string>.SuccessResult(
                            existingTokenResult.Data,
                            _localizationService.GetLocalizedString("Success.User.LoginSuccessful"));
                    }

                    activeSession.RevokedAt = DateTime.UtcNow;
                    activeSession.UpdatedDate = DateTime.UtcNow;
                    await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                    _userSessionCacheService.RemoveSession(activeSession.SessionId);
                }

                EnsureRefreshToken(user);

                var sessionId = Guid.NewGuid();
                var issuedAtUtc = DateTime.UtcNow;
                var tokenResponse = _jwtService.GenerateToken(user, sessionId, issuedAtUtc);
                if (!tokenResponse.Success)
                {
                    return ApiResponse<string>.ErrorResult(_localizationService.GetLocalizedString("Error.User.LoginFailed"), tokenResponse.Message ?? string.Empty, 500);
                }
                var token = tokenResponse.Data!;

                var session = new UserSession
                {
                    UserId = user.Id,
                    SessionId = sessionId,
                    CreatedAt = issuedAtUtc,
                    Token = ComputeSha256Hash(token),
                    IsDeleted = false,
                    CreatedDate = issuedAtUtc
                };
                await _unitOfWork.UserSessions.AddAsync(session).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                _userSessionCacheService.SetActiveSession(session.SessionId, session.UserId, GetSessionExpiryUtc(session.CreatedAt));
                
                return ApiResponse<string>.SuccessResult(token, _localizationService.GetLocalizedString("Success.User.LoginSuccessful"));
            }
            catch (Exception ex)
            {
                return ApiResponse<string>.ErrorResult(_localizationService.GetLocalizedString("Error.User.LoginFailed"), ex.Message ?? string.Empty, 500);
            }
        }

        public async Task<ApiResponse<UserDto>> GetUserByEmailOrUsernameAsync(string emailOrUsername)
        {
            try
            {
                var user = await _unitOfWork.Users.Query().Include(u => u.RoleNavigation).FirstOrDefaultAsync(u => (u.Email == emailOrUsername || u.Username == emailOrUsername) && u.IsActive).ConfigureAwait(false);
                
                if (user == null)
                {
                    var nf = _localizationService.GetLocalizedString("AuthUserNotFound");
                    return ApiResponse<UserDto>.ErrorResult(nf, nf, 404);
                }

                var dto = MapToUserDto(user);
                return ApiResponse<UserDto>.SuccessResult(dto, _localizationService.GetLocalizedString("AuthUserRetrievedSuccessfully"));
            }
            catch (Exception ex)
            {
                return ApiResponse<UserDto>.ErrorResult(_localizationService.GetLocalizedString("AuthErrorOccurred"), ex.Message ?? string.Empty, 500);
            }
        }

        public async Task<ApiResponse<LoginWithSessionResponseDto>> LoginWithSessionAsync(LoginDto loginDto)
        {
            try
            {
                var loginRequest = new LoginRequest
                {
                    Email = loginDto.Username,
                    Password = loginDto.Password,
                    RememberMe = loginDto.RememberMe
                };

                var loginResult = await LoginAsync(loginRequest).ConfigureAwait(false);
                if (!loginResult.Success)
                {
                    return ApiResponse<LoginWithSessionResponseDto>.ErrorResult(loginResult.Message, loginResult.ExceptionMessage ?? string.Empty, loginResult.StatusCode);
                }

                var userResult = await GetUserByEmailOrUsernameAsync(loginDto.Username).ConfigureAwait(false);
                if (!userResult.Success)
                {
                    return ApiResponse<LoginWithSessionResponseDto>.ErrorResult(userResult.Message, null, 401);
                }

                var user = await _unitOfWork.Users.Query().FirstOrDefaultAsync(u => u.Username == loginDto.Username || u.Email == loginDto.Username).ConfigureAwait(false);
                if (user == null)
                {
                    return ApiResponse<LoginWithSessionResponseDto>.ErrorResult(_localizationService.GetLocalizedString("AuthUserNotFound"), null, 404);
                }

                var session = await _unitOfWork.UserSessions.Query().FirstOrDefaultAsync(s => s.UserId == user.Id && s.RevokedAt == null).ConfigureAwait(false);
                if (session == null)
                {
                    return ApiResponse<LoginWithSessionResponseDto>.ErrorResult(_localizationService.GetLocalizedString("AuthSessionNotFound"), null, 404);
                }

                var response = new LoginWithSessionResponseDto
                {
                    Token = loginResult.Data!,
                    RefreshToken = user.RefreshToken ?? string.Empty,
                    RefreshTokenExpiresAt = user.RefreshTokenExpiryTime,
                    UserId = user.Id,
                    SessionId = session.SessionId,
                    RememberMe = loginDto.RememberMe
                };

                return ApiResponse<LoginWithSessionResponseDto>.SuccessResult(response, loginResult.Message);
            }
            catch (Exception ex)
            {
                return ApiResponse<LoginWithSessionResponseDto>.ErrorResult(
                    _localizationService.GetLocalizedString("Error.User.LoginFailed"),
                    ex.Message ?? string.Empty,
                    500);
            }
        }

        public async Task<ApiResponse<LoginWithSessionResponseDto>> RefreshTokenAsync(RefreshTokenDto request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.RefreshToken))
                {
                    var validationMessage = _localizationService.GetLocalizedString("General.ValidationError");
                    return ApiResponse<LoginWithSessionResponseDto>.ErrorResult(validationMessage, validationMessage, 400);
                }

                var now = DateTime.UtcNow;
                var user = await _unitOfWork.Users.Query(tracking: true)
                    .FirstOrDefaultAsync(u =>
                        u.RefreshToken == request.RefreshToken &&
                        u.RefreshTokenExpiryTime != null &&
                        u.RefreshTokenExpiryTime > now &&
                        u.IsActive)
                    .ConfigureAwait(false);

                if (user == null)
                {
                    var invalidMessage = _localizationService.GetLocalizedString("Error.User.InvalidCredentials");
                    return ApiResponse<LoginWithSessionResponseDto>.ErrorResult(invalidMessage, invalidMessage, 401);
                }

                var session = await _unitOfWork.UserSessions.Query(tracking: true)
                    .Where(s => s.UserId == user.Id && s.RevokedAt == null)
                    .OrderByDescending(s => s.CreatedAt)
                    .FirstOrDefaultAsync()
                    .ConfigureAwait(false);

                var sessionId = session?.SessionId ?? Guid.NewGuid();
                var issuedAtUtc = now;
                var tokenResponse = _jwtService.GenerateToken(user, sessionId, issuedAtUtc);
                if (!tokenResponse.Success || string.IsNullOrWhiteSpace(tokenResponse.Data))
                {
                    return ApiResponse<LoginWithSessionResponseDto>.ErrorResult(
                        _localizationService.GetLocalizedString("Error.User.LoginFailed"),
                        tokenResponse.ExceptionMessage ?? tokenResponse.Message ?? string.Empty,
                        500);
                }

                var accessToken = tokenResponse.Data!;
                if (session == null)
                {
                    session = new UserSession
                    {
                        UserId = user.Id,
                        SessionId = sessionId,
                        CreatedAt = issuedAtUtc,
                        Token = ComputeSha256Hash(accessToken),
                        IsDeleted = false,
                        CreatedDate = issuedAtUtc
                    };

                    await _unitOfWork.UserSessions.AddAsync(session).ConfigureAwait(false);
                }
                else
                {
                    session.CreatedAt = issuedAtUtc;
                    session.Token = ComputeSha256Hash(accessToken);
                    session.UpdatedDate = issuedAtUtc;
                    await _unitOfWork.UserSessions.UpdateAsync(session).ConfigureAwait(false);
                }

                EnsureRefreshToken(user);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                _userSessionCacheService.SetActiveSession(session.SessionId, session.UserId, GetSessionExpiryUtc(session.CreatedAt));

                var response = new LoginWithSessionResponseDto
                {
                    Token = accessToken,
                    RefreshToken = user.RefreshToken ?? string.Empty,
                    RefreshTokenExpiresAt = user.RefreshTokenExpiryTime,
                    UserId = user.Id,
                    SessionId = session.SessionId,
                    RememberMe = true
                };

                return ApiResponse<LoginWithSessionResponseDto>.SuccessResult(
                    response,
                    _localizationService.GetLocalizedString("Success.User.LoginSuccessful"));
            }
            catch (Exception ex)
            {
                return ApiResponse<LoginWithSessionResponseDto>.ErrorResult(
                    _localizationService.GetLocalizedString("Error.User.LoginFailed"),
                    ex.Message ?? string.Empty,
                    500);
            }
        }

        public async Task<ApiResponse<IEnumerable<UserDto>>> GetAllUsersAsync()
        {
            try
            {
                var users = await _unitOfWork.Users.Query().Include(u => u.RoleNavigation).ToListAsync().ConfigureAwait(false);
                var dtos = users.Select(MapToUserDto).ToList();
                return ApiResponse<IEnumerable<UserDto>>.SuccessResult(dtos, _localizationService.GetLocalizedString("DataRetrievedSuccessfully"));
            }
            catch (Exception ex)
            {
                return ApiResponse<IEnumerable<UserDto>>.ErrorResult(_localizationService.GetLocalizedString("AuthErrorOccurred"), ex.Message ?? string.Empty, 500);
            }
        }

        public async Task<ApiResponse<IEnumerable<UserDto>>> GetActiveUsersAsync()
        {
            try
            {
                var users = await _unitOfWork.Users.Query()
                    .Include(u => u.RoleNavigation)
                    .Where(u => u.IsActive == true)
                    .ToListAsync().ConfigureAwait(false);
                var dtos = users.Select(MapToUserDto).ToList();
                return ApiResponse<IEnumerable<UserDto>>.SuccessResult(dtos, _localizationService.GetLocalizedString("ActiveUsersRetrievedSuccessfully"));
            }
            catch (Exception ex)
            {
                return ApiResponse<IEnumerable<UserDto>>.ErrorResult(_localizationService.GetLocalizedString("AuthErrorOccurred"), ex.Message ?? string.Empty, 500);
            }
        }

        public async Task<ApiResponse<string>> RequestPasswordResetAsync(ForgotPasswordRequest request)
        {
            try
            {
                var user = await _unitOfWork.Users.Query().FirstOrDefaultAsync(u => u.Email == request.Email).ConfigureAwait(false);
                var token = Guid.NewGuid().ToString("N");
                var tokenHash = ComputeSha256Hash(token);
                var expiresAt = DateTime.UtcNow.AddMinutes(30);

                if (user != null)
                {
                    var reset = new PasswordResetRequest
                    {
                        UserId = user.Id,
                        TokenHash = tokenHash,
                        ExpiresAt = expiresAt,
                        CreatedDate = DateTime.UtcNow,
                        IsDeleted = false
                    };
                    await _unitOfWork.Repository<PasswordResetRequest>().AddAsync(reset).ConfigureAwait(false);
                    await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                    
                    var fullName = string.Join(" ", new[] { user.FirstName, user.LastName }.Where(x => !string.IsNullOrWhiteSpace(x)));
                    if (string.IsNullOrWhiteSpace(fullName))
                    {
                        fullName = user.Username;
                    }

                    var frontendBaseUrl = _configuration["FrontendSettings:BaseUrl"] ?? "http://localhost:5173";
                    var resetPasswordPath = _configuration["FrontendSettings:ResetPasswordPath"] ?? "/reset-password";
                    var resetLink = $"{frontendBaseUrl}{resetPasswordPath}?token={token}";

                    var emailSubject = _localizationService.GetLocalizedString("PasswordResetEmailSubject");
                    BackgroundJob.Enqueue<IMailJob>(job =>
                        job.SendPasswordResetEmailAsync(
                            user.Email,
                            fullName,
                            resetLink,
                            emailSubject));
                }

                var msg = _localizationService.GetLocalizedString("OperationSuccessful");
                return ApiResponse<string>.SuccessResult(string.Empty, msg);
            }
            catch (Exception ex)
            {
                return ApiResponse<string>.ErrorResult(_localizationService.GetLocalizedString("AuthErrorOccurred"), ex.Message ?? string.Empty, 500);
            }
        }

        public async Task<ApiResponse<bool>> ResetPasswordAsync(ResetPasswordRequest request)
        {
            try
            {
                var tokenHash = ComputeSha256Hash(request.Token);
                var now = DateTime.UtcNow;

                var reset = await _unitOfWork.Repository<PasswordResetRequest>().Query(tracking: true)
                    .Include(r => r.User)
                    .FirstOrDefaultAsync(r => r.TokenHash == tokenHash && r.UsedAt == null && r.ExpiresAt > now && !r.IsDeleted).ConfigureAwait(false);

                if (reset == null || reset.User == null)
                {
                    return ApiResponse<bool>.ErrorResult(_localizationService.GetLocalizedString("ValidationError"), _localizationService.GetLocalizedString("ValidationError"), 400);
                }

                reset.UsedAt = now;
                reset.UpdatedDate = now;

                reset.User.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
                reset.User.UpdatedDate = now;

                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                await InvalidateUserSessionsAsync(reset.User.Id).ConfigureAwait(false);

                var displayName = string.Join(" ", new[] { reset.User.FirstName, reset.User.LastName }.Where(x => !string.IsNullOrWhiteSpace(x)));
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    displayName = reset.User.Username;
                }
                var frontendBaseUrl = _configuration["FrontendSettings:BaseUrl"] ?? "http://localhost:5173";
                BackgroundJob.Enqueue<IMailJob>(job => job.SendPasswordResetCompletedEmailAsync(reset.User.Email, displayName, frontendBaseUrl));

                return ApiResponse<bool>.SuccessResult(true, _localizationService.GetLocalizedString("OperationSuccessful"));
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.ErrorResult(_localizationService.GetLocalizedString("AuthErrorOccurred"), ex.Message ?? string.Empty, 500);
            }
        }

        public async Task<ApiResponse<string>> ChangePasswordAsync(ChangePasswordRequest request)
        {
            try
            {
                var userIdRequest = await _userService.GetCurrentUserIdAsync().ConfigureAwait(false);
                if (!userIdRequest.Success)
                {
                    return ApiResponse<string>.ErrorResult(userIdRequest.Message, userIdRequest.ExceptionMessage ?? string.Empty, userIdRequest.StatusCode);
                }
                var userId = userIdRequest.Data!;
                var user = await _unitOfWork.Users.Query(tracking: true).FirstOrDefaultAsync(u => u.Id == userId).ConfigureAwait(false);
                if (user == null)
                {
                    var nf = _localizationService.GetLocalizedString("AuthUserNotFound");
                    return ApiResponse<string>.ErrorResult(nf, nf, 404);
                }

                if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
                {
                    var msg = _localizationService.GetLocalizedString("Error.User.CurrentPasswordIncorrect");
                    if (msg == "Error.User.CurrentPasswordIncorrect")
                    {
                        msg = _localizationService.GetLocalizedString("Error.User.InvalidCredentials");
                    }
                    return ApiResponse<string>.ErrorResult(msg, msg, 400);
                }

                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
                user.UpdatedDate = DateTime.UtcNow;
                var affectedRows = await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                if (affectedRows == 0 || !BCrypt.Net.BCrypt.Verify(request.NewPassword, user.PasswordHash))
                {
                    var saveMsg = _localizationService.GetLocalizedString("AuthErrorOccurred");
                    return ApiResponse<string>.ErrorResult(saveMsg, saveMsg, 500);
                }

                await InvalidateUserSessionsAsync(user.Id).ConfigureAwait(false);

                var sessionId = Guid.NewGuid();
                var issuedAtUtc = DateTime.UtcNow;
                var tokenResponse = _jwtService.GenerateToken(user, sessionId, issuedAtUtc);
                if (!tokenResponse.Success || string.IsNullOrWhiteSpace(tokenResponse.Data))
                {
                    return ApiResponse<string>.ErrorResult(
                        _localizationService.GetLocalizedString("Error.User.LoginFailed"),
                        tokenResponse.ExceptionMessage,
                        tokenResponse.StatusCode == 0 ? 500 : tokenResponse.StatusCode);
                }

                var newToken = tokenResponse.Data!;
                var session = new UserSession
                {
                    UserId = user.Id,
                    SessionId = sessionId,
                    CreatedAt = issuedAtUtc,
                    Token = ComputeSha256Hash(newToken),
                    IsDeleted = false,
                    CreatedDate = issuedAtUtc
                };
                await _unitOfWork.UserSessions.AddAsync(session).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                _userSessionCacheService.SetActiveSession(session.SessionId, session.UserId, GetSessionExpiryUtc(session.CreatedAt));

                var displayName = string.Join(" ", new[] { user.FirstName, user.LastName }.Where(x => !string.IsNullOrWhiteSpace(x)));
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    displayName = user.Username;
                }
                var frontendBaseUrl = _configuration["FrontendSettings:BaseUrl"] ?? "http://localhost:5173";
                BackgroundJob.Enqueue<IMailJob>(job => job.SendPasswordChangedEmailAsync(user.Email, displayName, frontendBaseUrl));

                return ApiResponse<string>.SuccessResult(newToken, _localizationService.GetLocalizedString("OperationSuccessful"));
            }
            catch (Exception ex)
            {
                return ApiResponse<string>.ErrorResult(_localizationService.GetLocalizedString("AuthErrorOccurred"), ex.Message ?? string.Empty, 500);
            }
        }

        static string ComputeSha256Hash(string rawData)
        {
            using var sha256Hash = System.Security.Cryptography.SHA256.Create();
            var bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
            var builder = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("x2"));
            }
            return builder.ToString();
        }

        private bool IsSessionTokenReusable(UserSession session)
        {
            return session.RevokedAt == null && GetSessionExpiryUtc(session.CreatedAt) > DateTime.UtcNow;
        }

        private DateTime GetSessionExpiryUtc(DateTime createdAtUtc)
        {
            return createdAtUtc.AddMinutes(GetJwtExpiryMinutes());
        }

        private void EnsureRefreshToken(User user)
        {
            if (!string.IsNullOrWhiteSpace(user.RefreshToken) &&
                user.RefreshTokenExpiryTime.HasValue &&
                user.RefreshTokenExpiryTime.Value > DateTime.UtcNow)
            {
                return;
            }

            user.RefreshToken = GenerateRefreshToken();
            user.RefreshTokenExpiryTime = DateTime.UtcNow.Add(RefreshTokenLifetime);
            user.UpdatedDate = DateTime.UtcNow;
        }

        private static string GenerateRefreshToken()
        {
            var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(64);
            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');
        }

        private double GetJwtExpiryMinutes()
        {
            var expiryValue = _configuration["JwtSettings:ExpiryMinutes"];
            return double.TryParse(expiryValue, out var expiryMinutes) && expiryMinutes > 0
                ? expiryMinutes
                : 60;
        }

        private async Task InvalidateUserSessionsAsync(long userId)
        {
            var sessions = await _unitOfWork.UserSessions.Query(tracking: true)
                .Where(s => s.UserId == userId && s.RevokedAt == null)
                .ToListAsync().ConfigureAwait(false);

            if (sessions.Count > 0)
            {
                var now = DateTime.UtcNow;
                foreach (var s in sessions)
                {
                    s.RevokedAt = now;
                    s.UpdatedDate = now;
                    _userSessionCacheService.RemoveSession(s.SessionId);
                }
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                await AuthHub.ForceLogoutUser(_hubContext, userId.ToString()).ConfigureAwait(false);
            }
        }

        static UserDto MapToUserDto(User user)
        {
            return new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                PhoneNumber = user.PhoneNumber,
                Role = user.RoleNavigation?.Title ?? "User",
                IsEmailConfirmed = user.IsEmailConfirmed,
                IsActive = user.IsActive,
                LastLoginDate = user.LastLoginDate,
                FullName = user.FullName,
                CreatedDate = user.CreatedDate,
                IsDeleted = user.IsDeleted
            };
        }
    }
}
