using crm_api.DTOs;
using crm_api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace crm_api.Controllers
{
    [ApiController]
    [Route("api/customer-mail/outlook")]
    [Authorize]
    public class OutlookCustomerMailController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IOutlookEntegrationService _outlookEntegrationService;

        public OutlookCustomerMailController(
            IUserService userService,
            IOutlookEntegrationService outlookEntegrationService)
        {
            _userService = userService;
            _outlookEntegrationService = outlookEntegrationService;
        }

        [HttpPost("send")]
        public async Task<ActionResult<ApiResponse<OutlookMailSendResultDto>>> Send(
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

            var result = await _outlookEntegrationService.SendMailAsync(currentUserIdResult.Data, dto, cancellationToken);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("logs")]
        public async Task<ActionResult<ApiResponse<PagedResponse<OutlookCustomerMailLogDto>>>> GetLogs(
            [FromQuery] OutlookCustomerMailLogQueryDto query,
            CancellationToken cancellationToken)
        {
            var currentUserIdResult = await _userService.GetCurrentUserIdAsync();
            if (!currentUserIdResult.Success)
            {
                var error = ApiResponse<PagedResponse<OutlookCustomerMailLogDto>>.ErrorResult(
                    currentUserIdResult.Message,
                    currentUserIdResult.ExceptionMessage,
                    currentUserIdResult.StatusCode);

                return StatusCode(error.StatusCode, error);
            }

            var result = await _outlookEntegrationService.GetCustomerMailLogsAsync(currentUserIdResult.Data, query, cancellationToken);
            return StatusCode(result.StatusCode, result);
        }
    }
}
