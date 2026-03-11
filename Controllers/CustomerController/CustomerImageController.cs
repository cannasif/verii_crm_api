using crm_api.DTOs;
using crm_api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace crm_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CustomerImageController : ControllerBase
    {
        private readonly ICustomerImageService _customerImageService;
        private readonly ILocalizationService _localizationService;

        public CustomerImageController(ICustomerImageService customerImageService, ILocalizationService localizationService)
        {
            _customerImageService = customerImageService;
            _localizationService = localizationService;
        }

        [HttpPost("upload/{customerId}")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadImages(
            [FromRoute] long customerId,
            List<IFormFile> files,
            [FromForm] List<string>? imageDescriptions = null)
        {
            if (files == null || files.Count == 0)
            {
                return BadRequest(ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("FileUploadService.FileRequired"),
                    _localizationService.GetLocalizedString("FileUploadService.NoFilesProvided"),
                    400));
            }

            var result = await _customerImageService.UploadImagesAsync(customerId, files, imageDescriptions);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("by-customer/{customerId}")]
        public async Task<IActionResult> GetByCustomerId(long customerId)
        {
            var result = await _customerImageService.GetByCustomerIdAsync(customerId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _customerImageService.DeleteAsync(id);
            return StatusCode(result.StatusCode, result);
        }
    }
}
