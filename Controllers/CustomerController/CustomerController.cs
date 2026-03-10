using crm_api.DTOs;
using crm_api.Interfaces;
using Hangfire;
using Infrastructure.BackgroundJobs.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace crm_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CustomerController : ControllerBase
    {
        private readonly ICustomerService _customerService;
        private readonly IBackgroundJobClient _backgroundJobClient;

        public CustomerController(ICustomerService customerService, IBackgroundJobClient backgroundJobClient)
        {
            _customerService = customerService;
            _backgroundJobClient = backgroundJobClient;
        }

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] PagedRequest request)
        {
            var result = await _customerService.GetAllCustomersAsync(request);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(long id)
        {
            var result = await _customerService.GetCustomerByIdAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("nearby")]
        [HttpGet("/api/Rota/customers")]
        public async Task<IActionResult> GetNearby([FromQuery] CustomerNearbyQueryDto query)
        {
            var result = await _customerService.GetNearbyCustomersAsync(query);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CustomerCreateDto customerCreateDto)
        {
            var result = await _customerService.CreateCustomerAsync(customerCreateDto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("mobile/create-from-ocr")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> CreateFromMobile([FromForm] CustomerCreateFromMobileDto request)
        {
            var result = await _customerService.CreateCustomerFromMobileAsync(request);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(long id, [FromBody] CustomerUpdateDto customerUpdateDto)
        {
            var result = await _customerService.UpdateCustomerAsync(id, customerUpdateDto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _customerService.DeleteCustomerAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("dedupe/candidates")]
        public async Task<IActionResult> GetDuplicateCandidates()
        {
            var result = await _customerService.GetDuplicateCandidatesAsync();
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("dedupe/merge")]
        public async Task<IActionResult> MergeCustomers([FromBody] CustomerMergeRequestDto request)
        {
            var result = await _customerService.MergeCustomersAsync(request);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("sync")]
        public IActionResult TriggerSync()
        {
            var jobId = _backgroundJobClient.Enqueue<ICustomerSyncJob>(job => job.ExecuteAsync());

            return Ok(new ApiResponse<CustomerSyncTriggerResponseDto>
            {
                Success = true,
                Message = "Customer sync queued successfully.",
                Data = new CustomerSyncTriggerResponseDto
                {
                    JobId = jobId,
                    Queue = "default",
                    EnqueuedAtUtc = DateTime.UtcNow
                },
                StatusCode = StatusCodes.Status200OK
            });
        }
    }
}
