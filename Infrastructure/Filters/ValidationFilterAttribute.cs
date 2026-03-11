using crm_api.DTOs;
using crm_api.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace crm_api.Infrastructure.Filters
{
    public class ValidationFilterAttribute : Attribute, IAsyncActionFilter
    {
        private readonly ILocalizationService _localizationService;

        public ValidationFilterAttribute(ILocalizationService localizationService)
        {
            _localizationService = localizationService;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (!context.ModelState.IsValid)
            {
                var errors = context.ModelState
                    .Where(x => x.Value?.Errors.Count > 0)
                    .SelectMany(x => x.Value!.Errors.Select(error =>
                        string.IsNullOrWhiteSpace(x.Key)
                            ? error.ErrorMessage
                            : $"{x.Key}: {error.ErrorMessage}"))
                    .ToList();

                var response = ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("General.RequiredFieldsNotFilled"),
                    _localizationService.GetLocalizedString("General.RequiredFieldsNotFilled"),
                    StatusCodes.Status400BadRequest);

                response.Errors = errors;
                context.Result = new BadRequestObjectResult(response);
                return;
            }

            await next();
        }
    }
}
