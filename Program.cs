using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics;
using crm_api.Data;
using crm_api.Interfaces;
using crm_api.Mappings;
using crm_api.Repositories;
using crm_api.Services;
using crm_api.UnitOfWork;
using crm_api.Hubs;
using crm_api.Helpers;
using System.Security.Claims;
using System.IO;
using System.Resources;
using System.Reflection;
using Microsoft.Extensions.FileProviders;
using Hangfire;
using Hangfire.SqlServer;
using Infrastructure.BackgroundJobs.Interfaces;
using Microsoft.Extensions.Caching.Memory;              // ✅ SMTP için (IMemoryCache)
using crm_api.Infrastructure.Startup;
using crm_api.Infrastructure;
using crm_api.Infrastructure.Filters;

var builder = WebApplication.CreateBuilder(args);

var configuredCorsOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()
    ?.Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Select(origin => origin.Trim().TrimEnd('/'))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray()
    ?? Array.Empty<string>();

if (configuredCorsOrigins.Length == 0)
{
    throw new InvalidOperationException("Cors:AllowedOrigins ayari bos birakilamaz.");
}

builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = true;
});

// Add services to the container.
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationFilterAttribute>();
});

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddScoped<ValidationFilterAttribute>();


// ✅ SMTP için: MemoryCache + DataProtection
builder.Services.AddMemoryCache();
var dataProtectionKeyPath =
    builder.Configuration["DataProtection:KeyPath"] ??
    Path.Combine(builder.Environment.ContentRootPath, "DataProtectionKeys");
Directory.CreateDirectory(dataProtectionKeyPath);
builder.Services
    .AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyPath))
    .SetApplicationName("V3RII_CRM");

// SignalR Configuration
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
});

// Entity Framework Configuration - Using SQL Server
builder.Services.AddDbContext<CmsDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.CommandTimeout(60);
    });
});

// Hangfire Configuration
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(builder.Configuration.GetConnectionString("DefaultConnection"), new SqlServerStorageOptions
    {
        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
        QueuePollInterval = TimeSpan.Zero,
        UseRecommendedIsolationLevel = true,
        DisableGlobalLocks = true
    }));

builder.Services.Configure<HangfireMonitoringOptions>(
    builder.Configuration.GetSection(HangfireMonitoringOptions.SectionName));

builder.Services.Configure<GeocodingOptions>(
    builder.Configuration.GetSection(GeocodingOptions.SectionName));
builder.Services.Configure<GoogleOptions>(
    builder.Configuration.GetSection(GoogleOptions.SectionName));

GlobalJobFilters.Filters.Add(new AutomaticRetryAttribute
{
    Attempts = 3,
    DelaysInSeconds = new[] { 60, 300, 900 },
    LogEvents = true,
    OnAttemptsExceeded = AttemptsExceededAction.Fail
});

builder.Services.AddHangfireServer(options =>
{
    options.Queues = new[] { "default", "dead-letter" };
});

// Creates the first admin user only when the DB is empty and BootstrapAdmin is configured.
builder.Services.AddHostedService<AdminBootstrapHostedService>();

// ERP Database Configuration - Using SQL Server
builder.Services.AddDbContext<ErpCmsDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("ErpConnection");
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.CommandTimeout(60);
    });
});

// AutoMapper Configuration - Automatically discover all mapping profiles in the assembly
builder.Services.AddAutoMapper(typeof(Program).Assembly);

// Register Core Services
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

// Register Authentication & Authorization Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IUserAuthorityService, UserAuthorityService>();

// Register Localization Services
builder.Services.AddScoped<ILocalizationService, LocalizationService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// Register ERP Services
builder.Services.AddScoped<IErpService, ErpService>();

// Geocoding (adres → enlem/boylam)
builder.Services.AddScoped<IGeocodingService, GeocodingService>();

// Register Customer Services
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ICustomerImageService, CustomerImageService>();
builder.Services.AddScoped<ICustomerTypeService, CustomerTypeService>();
builder.Services.AddScoped<ICountryService, CountryService>();
builder.Services.AddScoped<ICityService, CityService>();
builder.Services.AddScoped<IDistrictService, DistrictService>();
builder.Services.AddScoped<IContactService, ContactService>();
builder.Services.AddScoped<IShippingAddressService, ShippingAddressService>();
builder.Services.AddScoped<ICustomer360Service, Customer360Service>();

// Register Quotation Services
builder.Services.AddScoped<IQuotationService, QuotationService>();
builder.Services.AddScoped<ITempQuotattionService, TempQuotattionService>();
builder.Services.AddScoped<IQuotationLineService, QuotationLineService>();
builder.Services.AddScoped<IQuotationExchangeRateService, QuotationExchangeRateService>();
builder.Services.AddScoped<IQuotationNotesService, QuotationNotesService>();

// Register Demand Services
builder.Services.AddScoped<IDemandService, DemandService>();
builder.Services.AddScoped<IDemandLineService, DemandLineService>();
builder.Services.AddScoped<IDemandExchangeRateService, DemandExchangeRateService>();
builder.Services.AddScoped<IDemandNotesService, DemandNotesService>();

// Register Order Services
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IOrderLineService, OrderLineService>();
builder.Services.AddScoped<IOrderExchangeRateService, OrderExchangeRateService>();
builder.Services.AddScoped<IOrderNotesService, OrderNotesService>();

// Register Product Services
builder.Services.AddScoped<IProductPricingService, ProductPricingService>();
builder.Services.AddScoped<IProductPricingGroupByService, ProductPricingGroupByService>();

// Register User Services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IUserContextService, UserContextService>();
builder.Services.AddScoped<IPermissionAccessService, PermissionAccessService>();
builder.Services.AddScoped<IPermissionDefinitionService, PermissionDefinitionService>();
builder.Services.AddScoped<IPermissionGroupService, PermissionGroupService>();
builder.Services.AddScoped<IUserPermissionGroupService, UserPermissionGroupService>();
builder.Services.AddScoped<ISalesmen360Service, Salesmen360Service>();
builder.Services.AddScoped<IRevenueQualityService, RevenueQualityService>();
builder.Services.AddScoped<INextBestActionService, NextBestActionService>();
builder.Services.AddScoped<IUserSessionService, UserSessionService>();
builder.Services.AddScoped<IUserSessionCacheService, UserSessionCacheService>();
builder.Services.AddScoped<IUserDiscountLimitService, UserDiscountLimitService>();
builder.Services.AddScoped<IUserDetailService, UserDetailService>();

// Register Activity Services
builder.Services.AddScoped<IActivityService, ActivityService>();
builder.Services.AddScoped<IActivityImageService, ActivityImageService>();
builder.Services.AddScoped<IActivityTypeService, ActivityTypeService>();
builder.Services.AddScoped<IActivityMeetingTypeService, ActivityMeetingTypeService>();
builder.Services.AddScoped<IActivityTopicPurposeService, ActivityTopicPurposeService>();
builder.Services.AddScoped<IActivityShippingService, ActivityShippingService>();

// Register Payment Services
builder.Services.AddScoped<IPaymentTypeService, PaymentTypeService>();
builder.Services.AddScoped<ISalesTypeService, SalesTypeService>();

// Register Title Services
builder.Services.AddScoped<ITitleService, TitleService>();

// Register Pricing Rule Services
builder.Services.AddScoped<IPricingRuleHeaderService, PricingRuleHeaderService>();
builder.Services.AddScoped<IPricingRuleLineService, PricingRuleLineService>();
builder.Services.AddScoped<IPricingRuleSalesmanService, PricingRuleSalesmanService>();

// Register Document Serial Type Services
builder.Services.AddScoped<IDocumentSerialTypeService, DocumentSerialTypeService>();

// Register Stock Services
builder.Services.AddScoped<IStockService, StockService>();
builder.Services.AddScoped<IStockDetailService, StockDetailService>();
builder.Services.AddScoped<IStockImageService, StockImageService>();
builder.Services.AddScoped<IStockRelationService, StockRelationService>();

// Register Approval Services
builder.Services.AddScoped<IApprovalActionService, ApprovalActionService>();
builder.Services.AddScoped<IApprovalFlowService, ApprovalFlowService>();
builder.Services.AddScoped<IApprovalFlowStepService, ApprovalFlowStepService>();
builder.Services.AddScoped<IApprovalRequestService, ApprovalRequestService>();
builder.Services.AddScoped<IApprovalRoleGroupService, ApprovalRoleGroupService>();
builder.Services.AddScoped<IApprovalRoleService, ApprovalRoleService>();
builder.Services.AddScoped<IApprovalUserRoleService, ApprovalUserRoleService>();

// Register Mail Services
builder.Services.AddScoped<IMailService, MailService>();
builder.Services.AddScoped<IEncryptionService, AesGcmEncryptionService>();
builder.Services.AddScoped<ITenantGoogleOAuthSettingsService, TenantGoogleOAuthSettingsService>();
builder.Services.AddScoped<IGoogleOAuthService, GoogleOAuthService>();
builder.Services.AddScoped<IGoogleTokenService, GoogleTokenService>();
builder.Services.AddScoped<IGoogleCalendarService, GoogleCalendarService>();
builder.Services.AddScoped<IGoogleIntegrationLogService, GoogleIntegrationLogService>();
builder.Services.AddScoped<IGoogleGmailApiService, GoogleGmailApiService>();
builder.Services.AddScoped<IOutlookEntegrationService, OutlookEntegrationService>();

// ✅ SMTP Settings Service kaydı
builder.Services.AddScoped<ISmtpSettingsService, SmtpSettingsService>();

// Register Background Jobs
builder.Services.AddScoped<Infrastructure.BackgroundJobs.Interfaces.IStockSyncJob, Infrastructure.BackgroundJobs.StockSyncJob>();
builder.Services.AddScoped<Infrastructure.BackgroundJobs.Interfaces.ICustomerSyncJob, Infrastructure.BackgroundJobs.CustomerSyncJob>();
builder.Services.AddScoped<Infrastructure.BackgroundJobs.Interfaces.IMailJob, Infrastructure.BackgroundJobs.MailJob>();
builder.Services.AddScoped<Infrastructure.BackgroundJobs.Interfaces.IHangfireDeadLetterJob, Infrastructure.BackgroundJobs.HangfireDeadLetterJob>();

// Register File Upload Services
builder.Services.AddScoped<IFileUploadService, FileUploadService>();

// PDF Report Template (report-builder discipline)
builder.Services.Configure<crm_api.Infrastructure.PdfBuilderOptions>(
    builder.Configuration.GetSection(crm_api.Infrastructure.PdfBuilderOptions.SectionName));
builder.Services.PostConfigure<crm_api.Infrastructure.PdfBuilderOptions>(options =>
{
    if (string.IsNullOrWhiteSpace(options.LocalImageBasePath))
        options.LocalImageBasePath = builder.Environment.ContentRootPath;
});
builder.Services.AddScoped<crm_api.Interfaces.IPdfReportTemplateValidator, crm_api.Services.PdfReportTemplateValidator>();
builder.Services.AddScoped<crm_api.Interfaces.IPdfReportDocumentGeneratorService, crm_api.Services.PdfReportDocumentGeneratorService>();
builder.Services.AddScoped<crm_api.Interfaces.IPdfReportTemplateService, crm_api.Services.PdfReportTemplateService>();
builder.Services.AddScoped<crm_api.Interfaces.IPdfTemplateAssetService, crm_api.Services.PdfTemplateAssetService>();
builder.Services.AddScoped<crm_api.Interfaces.IPdfTablePresetService, crm_api.Services.PdfTablePresetService>();
// Legacy Report Template (backward compatibility; delegates to PDF generator)
builder.Services.AddScoped<IReportTemplateService, ReportTemplateService>();
builder.Services.AddScoped<IReportPdfGeneratorService, ReportPdfGeneratorService>();

// Report Builder (no allowlist; connection + datasource check + preview + CRUD)
builder.Services.AddScoped<IReportingConnectionService, crm_api.Services.ReportBuilderService.ReportingConnectionService>();
builder.Services.AddScoped<IReportingCatalogService, crm_api.Services.ReportBuilderService.ReportingCatalogService>();
builder.Services.AddScoped<IReportService, crm_api.Services.ReportBuilderService.ReportService>();
builder.Services.AddScoped<IReportPreviewService, crm_api.Services.ReportBuilderService.ReportPreviewService>();

// PowerBi CRUD Services
builder.Services.AddScoped<IPowerBIGroupService, PowerBIGroupService>();
builder.Services.AddScoped<IPowerBIReportDefinitionService, PowerBIReportDefinitionService>();
builder.Services.AddScoped<IPowerBIGroupReportDefinitionService, PowerBIGroupReportDefinitionService>();
builder.Services.AddScoped<IUserPowerBIGroupService, UserPowerBIGroupService>();
builder.Services.AddScoped<IPowerBIConfigurationService, PowerBIConfigurationService>();
builder.Services.AddScoped<IPowerBIEmbedService, PowerBIEmbedService>();
builder.Services.AddScoped<IPowerBIReportSyncService, PowerBIReportSyncService>();
builder.Services.AddScoped<IPowerBIReportRoleMappingService, PowerBIReportRoleMappingService>();

// PowerBi / Azure AD options (embed token)
builder.Services.Configure<crm_api.Infrastructure.AzureAdSettings>(
    builder.Configuration.GetSection(crm_api.Infrastructure.AzureAdSettings.SectionName));
builder.Services.Configure<crm_api.Infrastructure.PowerBISettings>(
    builder.Configuration.GetSection(crm_api.Infrastructure.PowerBISettings.SectionName));

// Add HttpContextAccessor for accessing HTTP context in services
builder.Services.AddHttpContextAccessor();

// Add HttpClient for external requests (e.g., image loading in PDF generation)
builder.Services.AddHttpClient();

// Localization Configuration
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// Request Localization Configuration
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[]
    {
        new CultureInfo("en-US"),
        new CultureInfo("tr-TR"),
        new CultureInfo("de-DE"),
        new CultureInfo("fr-FR"),
        new CultureInfo("es-ES"),
        new CultureInfo("it-IT")
    };

    options.DefaultRequestCulture = new RequestCulture("tr-TR");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;

    // Add custom request culture provider for x-language header
    options.RequestCultureProviders.Insert(0, new CustomHeaderRequestCultureProvider());
});

// CORS Configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
    {
        policy.WithOrigins(configuredCorsOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// JWT Authentication Configuration
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.SaveToken = true;

    var jwtSecret = builder.Configuration["JwtSettings:SecretKey"];
    if (string.IsNullOrWhiteSpace(jwtSecret))
    {
        var rm = new ResourceManager("crm_api.Resources.Messages", Assembly.GetExecutingAssembly());
        var msg = rm.GetString("General.JwtSecretRequired", new CultureInfo("tr-TR")) ?? "General.JwtSecretRequired";
        throw new InvalidOperationException(msg);
    }

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["JwtSettings:Issuer"] ?? "CmsWebApi",
        ValidAudience = builder.Configuration["JwtSettings:Audience"] ?? "CmsWebApiUsers",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ClockSkew = TimeSpan.Zero
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;

            if (!string.IsNullOrEmpty(accessToken) && (
                path.StartsWithSegments("/api/authHub") ||
                path.StartsWithSegments("/authHub") ||
                path.StartsWithSegments("/api/notificationHub") ||
                path.StartsWithSegments("/notificationHub")))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        },
        OnTokenValidated = async context =>
        {
            var loc = context.HttpContext.RequestServices.GetRequiredService<ILocalizationService>();
            var claims = context.Principal?.Claims;
            var userIdClaim = claims?.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (!long.TryParse(userIdClaim, out var userId))
            {
                context.Fail(loc.GetLocalizedString("Auth.TokenInvalidMissingUserId"));
                return;
            }
            var sessionClaim = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Sid)?.Value
                ?? claims?.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
            if (!Guid.TryParse(sessionClaim, out var sessionId))
            {
                context.Fail(loc.GetLocalizedString("Auth.TokenInvalidMissingSessionId"));
                return;
            }

            try
            {
                var cache = context.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
                var sessionCacheService = context.HttpContext.RequestServices.GetRequiredService<IUserSessionCacheService>();
                var cacheKey = sessionCacheService.GetCacheKey(sessionId);

                if (cache.TryGetValue<long>(cacheKey, out var cachedUserId))
                {
                    if (cachedUserId != userId)
                    {
                        context.Fail(loc.GetLocalizedString("Auth.SessionExpiredOrInvalid"));
                    }

                    return;
                }

                var restored = await sessionCacheService.RestoreSessionAsync(sessionId, userId, context.HttpContext.RequestAborted);
                if (!restored)
                {
                    context.Fail(loc.GetLocalizedString("Auth.SessionExpiredOrInvalid"));
                }
            }
            catch (Exception ex)
            {
                context.Fail(loc.GetLocalizedString("Auth.SessionValidationFailed", ex.Message));
            }
        }
    };
});

// Configure Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CRM Web API",
        Version = "v1",
        Description = "A comprehensive CRM Web API with JWT Authentication",
        Contact = new OpenApiContact
        {
            Name = "CRM API Team",
            Email = "support@crmapi.com"
        }
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityDefinition("Language", new OpenApiSecurityScheme
    {
        Description = "Language header for localization. Use 'tr' for Turkish or 'en' for English. Example: \"x-language: tr\"",
        Name = "x-language",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "ApiKey"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "oauth2",
                Name = "Bearer",
                In = ParameterLocation.Header
            },
            new List<string>()
        },
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Language"
                }
            },
            new List<string>()
        }
    });

    c.CustomSchemaIds(type => type.FullName);

    c.MapType<IFormFile>(() => new OpenApiSchema
    {
        Type = "string",
        Format = "binary"
    });

    c.ParameterFilter<FileUploadParameterFilter>();
    c.OperationFilter<FileUploadOperationFilter>();

    c.CustomOperationIds(apiDesc => apiDesc.ActionDescriptor.RouteValues["action"]);

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

GlobalJobFilters.Filters.Add(
    new HangfireJobStateFilter(
        app.Services.GetRequiredService<ILogger<HangfireJobStateFilter>>(),
        app.Services.GetRequiredService<IBackgroundJobClient>(),
        app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<HangfireMonitoringOptions>>(),
        app.Services.GetRequiredService<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>()));

// Migrations are intentionally run out-of-band (e.g., dotnet ef database update)

// Configure the HTTP request pipeline.

// ── Early CORS middleware ──────────────────────────────────────────────
// Handles preflight (OPTIONS) requests *before* any other middleware can
// short-circuit. For non-preflight requests it adds the CORS headers so
// that even 500 / exception-handler responses carry them.
var allowedCorsOrigins = new HashSet<string>(configuredCorsOrigins, StringComparer.OrdinalIgnoreCase);

app.Use(async (ctx, next) =>
{
    var origin = ctx.Request.Headers["Origin"].ToString();
    if (!string.IsNullOrEmpty(origin) && allowedCorsOrigins.Contains(origin))
    {
        ctx.Response.Headers.Append("Access-Control-Allow-Origin", origin);
        ctx.Response.Headers.Append("Access-Control-Allow-Credentials", "true");

        // Preflight
        if (HttpMethods.IsOptions(ctx.Request.Method))
        {
            ctx.Response.Headers.Append("Access-Control-Allow-Methods", "GET, POST, PUT, PATCH, DELETE, OPTIONS");
            ctx.Response.Headers.Append("Access-Control-Allow-Headers",
                "Content-Type, Authorization, X-Branch-Code, Branch-Code, X-Language, x-language, x-branch-code");
            ctx.Response.Headers.Append("Access-Control-Max-Age", "86400");
            ctx.Response.StatusCode = 204;
            return; // short-circuit – don't call next
        }
    }

    await next();
});

// Ensure 500 from unhandled exceptions still get CORS headers (browser would otherwise hide the response)
app.UseExceptionHandler(errApp =>
{
    errApp.Run(async ctx =>
    {
        var ex = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
        var logger = ctx.RequestServices.GetService<ILogger<Program>>();
        var localizationService = ctx.RequestServices.GetRequiredService<ILocalizationService>();
        if (ex != null)
            logger?.LogError(ex, "Unhandled exception: {Path}", ctx.Request.Path);

        var dbUpdateException = FindDbUpdateException(ex);
        if (dbUpdateException != null &&
            DbUpdateExceptionHelper.TryGetUniqueViolation(dbUpdateException, out _))
        {
            var isCountryPath = ctx.Request.Path.StartsWithSegments("/api/Country", StringComparison.OrdinalIgnoreCase);
            var localizedMessage = isCountryPath
                ? localizationService.GetLocalizedString("CountryService.CountryNameAlreadyExists")
                : localizationService.GetLocalizedString("General.RecordAlreadyExists");

            var response = crm_api.DTOs.ApiResponse<object>.ErrorResult(
                localizedMessage,
                null,
                StatusCodes.Status409Conflict);
            response.Errors = new List<string> { localizedMessage };
            response.Timestamp = DateTime.UtcNow;
            response.ExceptionMessage = null!;
            if (isCountryPath)
            {
                response.ClassName = "ApiResponse<CountryGetDto>";
            }

            ctx.Response.StatusCode = StatusCodes.Status409Conflict;
            ctx.Response.ContentType = "application/json";
            var conflictOrigin = ctx.Request.Headers["Origin"].ToString();
            if (!string.IsNullOrEmpty(conflictOrigin) && allowedCorsOrigins.Contains(conflictOrigin))
            {
                if (!ctx.Response.Headers.ContainsKey("Access-Control-Allow-Origin"))
                {
                    ctx.Response.Headers.Append("Access-Control-Allow-Origin", conflictOrigin);
                    ctx.Response.Headers.Append("Access-Control-Allow-Credentials", "true");
                }
            }
            var conflictJson = System.Text.Json.JsonSerializer.Serialize(response);
            await ctx.Response.WriteAsync(conflictJson);
            return;
        }

        ctx.Response.StatusCode = 500;
        ctx.Response.ContentType = "application/json";
        var origin = ctx.Request.Headers["Origin"].ToString();
        if (!string.IsNullOrEmpty(origin) && allowedCorsOrigins.Contains(origin))
        {
            // Headers may already be set by the early middleware, but Append is
            // safe – duplicates are ignored when the value already exists.
            if (!ctx.Response.Headers.ContainsKey("Access-Control-Allow-Origin"))
            {
                ctx.Response.Headers.Append("Access-Control-Allow-Origin", origin);
                ctx.Response.Headers.Append("Access-Control-Allow-Credentials", "true");
            }
        }
        var fallbackError = localizationService.GetLocalizedString("General.InternalServerError");
        var message = ex?.Message ?? fallbackError;
        var json = System.Text.Json.JsonSerializer.Serialize(new { error = fallbackError, message });
        await ctx.Response.WriteAsync(json);
    });
});

static DbUpdateException? FindDbUpdateException(Exception? exception)
{
    var current = exception;
    while (current != null)
    {
        if (current is DbUpdateException dbUpdateException)
        {
            return dbUpdateException;
        }

        current = current.InnerException;
    }

    return null;
}

app.UseRouting();

app.UseCors("DevCors");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "CRM Web API v1");
        c.RoutePrefix = "swagger";
    });
}

// Static files for uploaded images - wwwroot folder (default)
app.UseStaticFiles();

// Static files for uploads folder (project root/uploads)
var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "uploads");
if (Directory.Exists(uploadsPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(uploadsPath),
        RequestPath = "/uploads"
    });
}

// Add Request Localization Middleware
app.UseRequestLocalization();

// Add BranchCode Middleware
app.UseMiddleware<BranchCodeMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

// Endpoint mapping
app.MapHub<AuthHub>("/authHub");
app.MapHub<crm_api.Hubs.NotificationHub>("/notificationHub");
app.MapControllers();

// Hangfire Dashboard
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() }
});

// Register Recurring Jobs
if (!app.Environment.IsDevelopment())
{
    RecurringJob.AddOrUpdate<IStockSyncJob>(
        "erp-stock-sync-job",
        job => job.ExecuteAsync(),
        Cron.MinuteInterval(30));
    RecurringJob.AddOrUpdate<ICustomerSyncJob>(
        "erp-customer-sync-job",
        job => job.ExecuteAsync(),
        Cron.MinuteInterval(30));
}
else
{
    RecurringJob.RemoveIfExists("erp-stock-sync-job");
    RecurringJob.RemoveIfExists("erp-customer-sync-job");
    app.Logger.LogInformation("Skipping recurring ERP sync jobs in Development environment.");
}

app.Run();

public partial class Program { }
