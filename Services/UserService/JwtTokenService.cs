using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using crm_api.Models;
using crm_api.Interfaces;
using crm_api.DTOs;
using crm_api.UnitOfWork;

namespace crm_api.Services
{
    public class JwtService : IJwtService
    {
        private readonly IConfiguration _configuration;
        private readonly ILocalizationService _localizationService;

        public JwtService(IConfiguration configuration, ILocalizationService localizationService)
        {
            _configuration = configuration;
            _localizationService = localizationService;
        }

        public ApiResponse<string> GenerateToken(User user, Guid sessionId)
        {
            try
            {
                var claims = new[]
                {
                    new Claim(JwtRegisteredClaimNames.Jti, sessionId.ToString()),
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Sid, sessionId.ToString()),
                    new Claim("firstName", user.FirstName ?? ""),
                    new Claim("lastName", user.LastName ?? ""),
                    new Claim(ClaimTypes.Role, user.RoleNavigation?.Title ?? "User"),
                    new Claim("RoleId", user.RoleId.ToString())
                };

                // Read settings from "JwtSettings" to align with Program.cs and appsettings.json
                var jwtSettings = _configuration.GetSection("JwtSettings");
                var secret = jwtSettings["SecretKey"];
                var issuer = jwtSettings["Issuer"];
                var audience = jwtSettings["Audience"];
                var expiryMinutesStr = jwtSettings["ExpiryMinutes"] ?? "60";

                if (string.IsNullOrWhiteSpace(secret))
                {
                    return ApiResponse<string>.ErrorResult(
                        _localizationService.GetLocalizedString("JwtService.TokenGenerationError"),
                        _localizationService.GetLocalizedString("JwtService.MissingSecretKeyExceptionMessage", "JwtSettings:SecretKey"),
                        500);
                }

                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
                var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                var expires = DateTime.UtcNow.AddMinutes(Convert.ToDouble(expiryMinutesStr));

                var token = new JwtSecurityToken(
                    issuer: issuer,
                    audience: audience,
                    claims: claims,
                    expires: expires,
                    signingCredentials: credentials
                );

                var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
                return ApiResponse<string>.SuccessResult(tokenString, _localizationService.GetLocalizedString("JwtService.TokenGeneratedSuccessfully"));
            }
            catch (Exception ex)
            {
                return ApiResponse<string>.ErrorResult(
                    _localizationService.GetLocalizedString("JwtService.TokenGenerationError"),
                    _localizationService.GetLocalizedString("JwtService.TokenGenerationExceptionMessage", ex.Message),
                    500);
            }
        }
    }
}
