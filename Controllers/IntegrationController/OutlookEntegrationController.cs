using crm_api.DTOs;
using crm_api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace crm_api.Controllers
{
    [ApiController]
    [Route("api/integrations/outlook")]
    [Authorize]
    public class OutlookEntegrationController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IOutlookEntegrationService _outlookEntegrationService;
        private readonly IConfiguration _configuration;

        public OutlookEntegrationController(
            IUserService userService,
            IOutlookEntegrationService outlookEntegrationService,
            IConfiguration configuration)
        {
            _userService = userService;
            _outlookEntegrationService = outlookEntegrationService;
            _configuration = configuration;
        }

        [HttpGet("authorize-url")]
        public async Task<ActionResult<ApiResponse<OutlookEntegrationAuthorizeUrlDto>>> GetAuthorizeUrl(CancellationToken cancellationToken)
        {
            var currentUserIdResult = await _userService.GetCurrentUserIdAsync();
            if (!currentUserIdResult.Success)
            {
                var error = ApiResponse<OutlookEntegrationAuthorizeUrlDto>.ErrorResult(
                    currentUserIdResult.Message,
                    currentUserIdResult.ExceptionMessage,
                    currentUserIdResult.StatusCode);

                return StatusCode(error.StatusCode, error);
            }

            var response = await _outlookEntegrationService.CreateConnectUrlAsync(currentUserIdResult.Data, cancellationToken);
            return StatusCode(response.StatusCode, response);
        }

        [AllowAnonymous]
        [HttpGet("callback")]
        public async Task<IActionResult> Callback(
            [FromQuery] string? code,
            [FromQuery] string? state,
            [FromQuery] string? error,
            CancellationToken cancellationToken)
        {
            var response = await _outlookEntegrationService.HandleOAuthCallbackAsync(code, state, error, cancellationToken);
            return Redirect(BuildFrontendRedirect(response.Success, response.Success ? null : response.ExceptionMessage ?? response.Message));
        }

        [HttpGet("status")]
        public async Task<ActionResult<ApiResponse<OutlookEntegrationStatusDto>>> GetStatus(CancellationToken cancellationToken)
        {
            var currentUserIdResult = await _userService.GetCurrentUserIdAsync();
            if (!currentUserIdResult.Success)
            {
                var error = ApiResponse<OutlookEntegrationStatusDto>.ErrorResult(
                    currentUserIdResult.Message,
                    currentUserIdResult.ExceptionMessage,
                    currentUserIdResult.StatusCode);

                return StatusCode(error.StatusCode, error);
            }

            var response = await _outlookEntegrationService.GetStatusAsync(currentUserIdResult.Data, cancellationToken);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost("disconnect")]
        public async Task<ActionResult<ApiResponse<bool>>> Disconnect(CancellationToken cancellationToken)
        {
            var currentUserIdResult = await _userService.GetCurrentUserIdAsync();
            if (!currentUserIdResult.Success)
            {
                var error = ApiResponse<bool>.ErrorResult(
                    currentUserIdResult.Message,
                    currentUserIdResult.ExceptionMessage,
                    currentUserIdResult.StatusCode);

                return StatusCode(error.StatusCode, error);
            }

            var response = await _outlookEntegrationService.DisconnectAsync(currentUserIdResult.Data, cancellationToken);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost("send-mail")]
        public async Task<ActionResult<ApiResponse<OutlookMailSendResultDto>>> SendMail(
            [FromBody] SendOutlookMailDto dto,
            CancellationToken cancellationToken)
        {
            var currentUserIdResult = await _userService.GetCurrentUserIdAsync();
            if (!currentUserIdResult.Success)
            {
                var error = ApiResponse<OutlookMailSendResultDto>.ErrorResult(
                    currentUserIdResult.Message,
                    currentUserIdResult.ExceptionMessage,
                    currentUserIdResult.StatusCode);

                return StatusCode(error.StatusCode, error);
            }

            var response = await _outlookEntegrationService.SendMailAsync(currentUserIdResult.Data, dto, cancellationToken);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost("calendar/events")]
        public async Task<ActionResult<ApiResponse<OutlookCalendarEventResultDto>>> CreateCalendarEvent(
            [FromBody] CreateOutlookCalendarEventDto dto,
            CancellationToken cancellationToken)
        {
            var currentUserIdResult = await _userService.GetCurrentUserIdAsync();
            if (!currentUserIdResult.Success)
            {
                var error = ApiResponse<OutlookCalendarEventResultDto>.ErrorResult(
                    currentUserIdResult.Message,
                    currentUserIdResult.ExceptionMessage,
                    currentUserIdResult.StatusCode);

                return StatusCode(error.StatusCode, error);
            }

            var response = await _outlookEntegrationService.CreateCalendarEventAsync(currentUserIdResult.Data, dto, cancellationToken);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPut("calendar/events/{eventId}")]
        public async Task<ActionResult<ApiResponse<OutlookCalendarEventResultDto>>> UpdateCalendarEvent(
            [FromRoute] string eventId,
            [FromBody] UpdateOutlookCalendarEventDto dto,
            CancellationToken cancellationToken)
        {
            var currentUserIdResult = await _userService.GetCurrentUserIdAsync();
            if (!currentUserIdResult.Success)
            {
                var error = ApiResponse<OutlookCalendarEventResultDto>.ErrorResult(
                    currentUserIdResult.Message,
                    currentUserIdResult.ExceptionMessage,
                    currentUserIdResult.StatusCode);

                return StatusCode(error.StatusCode, error);
            }

            var response = await _outlookEntegrationService.UpdateCalendarEventAsync(currentUserIdResult.Data, eventId, dto, cancellationToken);
            return StatusCode(response.StatusCode, response);
        }

        [HttpDelete("calendar/events/{eventId}")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteCalendarEvent(
            [FromRoute] string eventId,
            CancellationToken cancellationToken)
        {
            var currentUserIdResult = await _userService.GetCurrentUserIdAsync();
            if (!currentUserIdResult.Success)
            {
                var error = ApiResponse<bool>.ErrorResult(
                    currentUserIdResult.Message,
                    currentUserIdResult.ExceptionMessage,
                    currentUserIdResult.StatusCode);

                return StatusCode(error.StatusCode, error);
            }

            var response = await _outlookEntegrationService.DeleteCalendarEventAsync(currentUserIdResult.Data, eventId, cancellationToken);
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("logs")]
        public async Task<ActionResult<ApiResponse<PagedResponse<OutlookEntegrationLogDto>>>> GetLogs(
            [FromQuery] OutlookEntegrationLogsQueryDto query,
            CancellationToken cancellationToken)
        {
            var currentUserIdResult = await _userService.GetCurrentUserIdAsync();
            if (!currentUserIdResult.Success)
            {
                var error = ApiResponse<PagedResponse<OutlookEntegrationLogDto>>.ErrorResult(
                    currentUserIdResult.Message,
                    currentUserIdResult.ExceptionMessage,
                    currentUserIdResult.StatusCode);

                return StatusCode(error.StatusCode, error);
            }

            var response = await _outlookEntegrationService.GetLogsAsync(currentUserIdResult.Data, query, cancellationToken);
            return StatusCode(response.StatusCode, response);
        }

        private string BuildFrontendRedirect(bool connected, string? error = null)
        {
            var baseUrl = _configuration["FrontendSettings:BaseUrl"]?.TrimEnd('/');
            var callbackPath = "/settings/integrations/outlook";
            var redirectBase = string.IsNullOrWhiteSpace(baseUrl)
                ? callbackPath
                : $"{baseUrl}{callbackPath}";

            var query = new Dictionary<string, string?>
            {
                ["connected"] = connected ? "1" : "0",
            };

            if (!connected && !string.IsNullOrWhiteSpace(error))
            {
                query["error"] = error.Length > 200 ? error[..200] : error;
            }

            return QueryHelpers.AddQueryString(redirectBase, query!);
        }
    }
}
