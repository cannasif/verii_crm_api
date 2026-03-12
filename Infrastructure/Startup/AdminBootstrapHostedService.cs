using crm_api.Data;
using crm_api.Models;
using Microsoft.EntityFrameworkCore;

namespace crm_api.Infrastructure.Startup
{
    public class AdminBootstrapHostedService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<AdminBootstrapHostedService> _logger;

        public AdminBootstrapHostedService(
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            IWebHostEnvironment environment,
            ILogger<AdminBootstrapHostedService> logger)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _environment = environment;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var email = _configuration["BootstrapAdmin:Email"];
            var password = _configuration["BootstrapAdmin:Password"];

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                return;
            }

            await using var scope = _serviceProvider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();

            var anyUsers = await db.Set<User>()
                .AsNoTracking()
                .AnyAsync(x => !x.IsDeleted, cancellationToken);

            if (anyUsers)
            {
                return;
            }

            var roleId = 3L;
            var roleIdStr = _configuration["BootstrapAdmin:RoleId"];
            if (!string.IsNullOrWhiteSpace(roleIdStr) && long.TryParse(roleIdStr, out var parsed))
            {
                roleId = parsed;
            }

            var normalizedEmail = email.Trim();

            var user = new User
            {
                Username = normalizedEmail,
                Email = normalizedEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                FirstName = "Admin",
                LastName = "User",
                RoleId = roleId,
                IsEmailConfirmed = true,
                IsActive = true,
                IsDeleted = false,
                CreatedDate = DateTimeProvider.Now
            };

            db.Set<User>().Add(user);
            await db.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "Bootstrap admin user created in {Environment}. Remove BootstrapAdmin credentials after first login.",
                _environment.EnvironmentName);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

