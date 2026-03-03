using crm_api.DTOs;
using crm_api.Infrastructure;
using crm_api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace crm_api.Controllers
{
    [ApiController]
    [Route("api/integrations/google")]
    [Authorize]
    public class GoogleIntegrationController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IGoogleOAuthService _googleOAuthService;
        private readonly IGoogleTokenService _googleTokenService;
        private readonly IGoogleCalendarService _googleCalendarService;
        private readonly IEncryptionService _encryptionService;
        private readonly GoogleOptions _googleOptions;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GoogleIntegrationController> _logger;

        public GoogleIntegrationController(
            IUserService userService,
            IGoogleOAuthService googleOAuthService,
            IGoogleTokenService googleTokenService,
            IGoogleCalendarService googleCalendarService,
            IEncryptionService encryptionService,
            IOptions<GoogleOptions> googleOptions,
            IConfiguration configuration,
            ILogger<GoogleIntegrationController> logger)
        {
            _userService = userService;
            _googleOAuthService = googleOAuthService;
            _googleTokenService = googleTokenService;
            _googleCalendarService = googleCalendarService;
            _encryptionService = encryptionService;
            _googleOptions = googleOptions.Value;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpGet("status")]
        public async Task<ActionResult<ApiResponse<GoogleIntegrationStatusDto>>> GetStatus(CancellationToken cancellationToken)
        {
            var currentUserIdResult = await _userService.GetCurrentUserIdAsync();
            if (!currentUserIdResult.Success)
            {
                var error = ApiResponse<GoogleIntegrationStatusDto>.ErrorResult(
                    currentUserIdResult.Message,
                    currentUserIdResult.ExceptionMessage,
                    currentUserIdResult.StatusCode);

                return StatusCode(error.StatusCode, error);
            }

            var account = await _googleTokenService.GetAccountAsync(currentUserIdResult.Data, cancellationToken);
            var dto = new GoogleIntegrationStatusDto
            {
                IsConnected = account?.IsConnected == true,
                GoogleEmail = account?.GoogleEmail,
                Scopes = account?.Scopes,
                ExpiresAt = account?.ExpiresAt,
            };

            var response = ApiResponse<GoogleIntegrationStatusDto>.SuccessResult(dto, "Google integration status retrieved.");
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("authorize-url")]
        public async Task<ActionResult<ApiResponse<GoogleAuthorizeUrlDto>>> GetAuthorizeUrl(CancellationToken cancellationToken)
        {
            var currentUserIdResult = await _userService.GetCurrentUserIdAsync();
            if (!currentUserIdResult.Success)
            {
                var error = ApiResponse<GoogleAuthorizeUrlDto>.ErrorResult(
                    currentUserIdResult.Message,
                    currentUserIdResult.ExceptionMessage,
                    currentUserIdResult.StatusCode);

                return StatusCode(error.StatusCode, error);
            }

            try
            {
                var url = await _googleOAuthService.CreateAuthorizeUrlAsync(currentUserIdResult.Data, cancellationToken);
                var response = ApiResponse<GoogleAuthorizeUrlDto>.SuccessResult(new GoogleAuthorizeUrlDto { Url = url }, "Google authorize URL created.");
                return StatusCode(response.StatusCode, response);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Google authorize URL configuration error for user {UserId}", currentUserIdResult.Data);
                var error = ApiResponse<GoogleAuthorizeUrlDto>.ErrorResult(
                    "Google authorize URL could not be generated.",
                    ex.Message,
                    StatusCodes.Status400BadRequest);

                return StatusCode(error.StatusCode, error);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create Google authorize URL for user {UserId}", currentUserIdResult.Data);
                var error = ApiResponse<GoogleAuthorizeUrlDto>.ErrorResult(
                    "Google authorize URL could not be generated.",
                    ex.Message,
                    StatusCodes.Status500InternalServerError);

                return StatusCode(error.StatusCode, error);
            }
        }

        [AllowAnonymous]
        [HttpGet("callback")]
        public async Task<IActionResult> Callback(
            [FromQuery] string? code,
            [FromQuery] string? state,
            [FromQuery] string? error,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(error))
            {
                return Redirect(BuildFrontendRedirect(false, error));
            }

            if (string.IsNullOrWhiteSpace(state) || !_googleOAuthService.TryExtractUserIdFromState(state, out var userId))
            {
                return Redirect(BuildFrontendRedirect(false, "invalid_state"));
            }

            var stateValid = await _googleOAuthService.ValidateAndConsumeStateAsync(userId, state);
            if (!stateValid)
            {
                return Redirect(BuildFrontendRedirect(false, "state_expired"));
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                return Redirect(BuildFrontendRedirect(false, "missing_code"));
            }

            try
            {
                var tokenResult = await _googleOAuthService.ExchangeCodeForTokensAsync(code, cancellationToken);
                var googleEmail = await _googleOAuthService.GetGoogleEmailAsync(
                    tokenResult.AccessToken,
                    tokenResult.IdToken,
                    cancellationToken);

                await _googleTokenService.UpsertConnectionAsync(
                    userId,
                    tokenResult,
                    googleEmail,
                    _googleOptions.Scopes,
                    cancellationToken);

                return Redirect(BuildFrontendRedirect(true));
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Google callback validation/token flow failed for user {UserId}", userId);
                var reason = ex.Message;
                if (reason.Length > 200)
                {
                    reason = reason[..200];
                }

                return Redirect(BuildFrontendRedirect(false, reason));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Google callback failed for user {UserId}", userId);
                return Redirect(BuildFrontendRedirect(false, "oauth_callback_failed"));
            }
        }

        [HttpPost("disconnect")]
        public async Task<ActionResult<ApiResponse<object>>> Disconnect(CancellationToken cancellationToken)
        {
            var currentUserIdResult = await _userService.GetCurrentUserIdAsync();
            if (!currentUserIdResult.Success)
            {
                var error = ApiResponse<object>.ErrorResult(
                    currentUserIdResult.Message,
                    currentUserIdResult.ExceptionMessage,
                    currentUserIdResult.StatusCode);

                return StatusCode(error.StatusCode, error);
            }

            try
            {
                var account = await _googleTokenService.GetAccountAsync(currentUserIdResult.Data, cancellationToken);
                if (account != null)
                {
                    var tokenToRevoke = TryDecrypt(account.RefreshTokenEncrypted)
                        ?? TryDecrypt(account.AccessTokenEncrypted);

                    if (!string.IsNullOrWhiteSpace(tokenToRevoke))
                    {
                        await _googleOAuthService.RevokeTokenAsync(tokenToRevoke, cancellationToken);
                    }
                }

                await _googleTokenService.DisconnectAsync(currentUserIdResult.Data, cancellationToken);

                var response = ApiResponse<object>.SuccessResult(null, "Google integration disconnected.");
                return StatusCode(response.StatusCode, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Google disconnect failed for user {UserId}", currentUserIdResult.Data);
                var error = ApiResponse<object>.ErrorResult(
                    "Google integration could not be disconnected.",
                    ex.Message,
                    StatusCodes.Status500InternalServerError);

                return StatusCode(error.StatusCode, error);
            }
        }

        [HttpPost("test-event")]
        public async Task<ActionResult<ApiResponse<GoogleTestEventDto>>> CreateTestEvent(CancellationToken cancellationToken)
        {
            var currentUserIdResult = await _userService.GetCurrentUserIdAsync();
            if (!currentUserIdResult.Success)
            {
                var error = ApiResponse<GoogleTestEventDto>.ErrorResult(
                    currentUserIdResult.Message,
                    currentUserIdResult.ExceptionMessage,
                    currentUserIdResult.StatusCode);

                return StatusCode(error.StatusCode, error);
            }

            try
            {
                var eventId = await _googleCalendarService.CreateTestEventAsync(currentUserIdResult.Data, cancellationToken);
                var response = ApiResponse<GoogleTestEventDto>.SuccessResult(
                    new GoogleTestEventDto { EventId = eventId },
                    "Google calendar test event created.");

                return StatusCode(response.StatusCode, response);
            }
            catch (InvalidOperationException ex)
            {
                var error = ApiResponse<GoogleTestEventDto>.ErrorResult(
                    "Google calendar test event could not be created.",
                    ex.Message,
                    StatusCodes.Status400BadRequest);

                return StatusCode(error.StatusCode, error);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Google test event creation failed for user {UserId}", currentUserIdResult.Data);
                var error = ApiResponse<GoogleTestEventDto>.ErrorResult(
                    "Google calendar test event could not be created.",
                    ex.Message,
                    StatusCodes.Status500InternalServerError);

                return StatusCode(error.StatusCode, error);
            }
        }

        private string BuildFrontendRedirect(bool connected, string? error = null)
        {
            var baseUrl = _configuration["FrontendSettings:BaseUrl"]?.TrimEnd('/');
            var callbackPath = "/settings/integrations/google";
            var redirectBase = string.IsNullOrWhiteSpace(baseUrl)
                ? callbackPath
                : $"{baseUrl}{callbackPath}";

            var query = new Dictionary<string, string?>
            {
                ["connected"] = connected ? "1" : "0",
            };

            if (!connected && !string.IsNullOrWhiteSpace(error))
            {
                query["error"] = error;
            }

            return QueryHelpers.AddQueryString(redirectBase, query!);
        }

        private string? TryDecrypt(string? encryptedValue)
        {
            if (string.IsNullOrWhiteSpace(encryptedValue))
            {
                return null;
            }

            try
            {
                return _encryptionService.Decrypt(encryptedValue);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to decrypt token during Google disconnect.");
                return null;
            }
        }
    }
}
