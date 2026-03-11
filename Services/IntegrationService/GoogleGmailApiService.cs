using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using crm_api.DTOs;
using crm_api.Helpers;
using crm_api.Interfaces;
using crm_api.Models;
using crm_api.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace crm_api.Services
{
    public class GoogleGmailApiService : IGoogleGmailApiService
    {
        private const string GmailSendEndpoint = "https://gmail.googleapis.com/gmail/v1/users/me/messages/send";
        private const string GmailSendScope = "https://www.googleapis.com/auth/gmail.send";

        private static readonly IReadOnlyDictionary<string, string> LogColumnMapping =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = "Id",
                ["customerId"] = "CustomerId",
                ["contactId"] = "ContactId",
                ["sentByUserId"] = "SentByUserId",
                ["provider"] = "Provider",
                ["toEmails"] = "ToEmails",
                ["subject"] = "Subject",
                ["isSuccess"] = "IsSuccess",
                ["errorCode"] = "ErrorCode",
                ["googleMessageId"] = "GoogleMessageId",
                ["googleThreadId"] = "GoogleThreadId",
                ["sentAt"] = "SentAt",
                ["createdDate"] = "CreatedDate",
            };

        private readonly IUnitOfWork _uow;
        private readonly ITenantGoogleOAuthSettingsService _tenantGoogleOAuthSettingsService;
        private readonly IGoogleTokenService _googleTokenService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IUserContextService _userContextService;
        private readonly IGoogleIntegrationLogService _googleIntegrationLogService;
        private readonly ILogger<GoogleGmailApiService> _logger;
        private readonly ILocalizationService _localizationService;

        public GoogleGmailApiService(
            IUnitOfWork uow,
            ITenantGoogleOAuthSettingsService tenantGoogleOAuthSettingsService,
            IGoogleTokenService googleTokenService,
            IHttpClientFactory httpClientFactory,
            IUserContextService userContextService,
            IGoogleIntegrationLogService googleIntegrationLogService,
            ILogger<GoogleGmailApiService> logger,
            ILocalizationService localizationService)
        {
            _uow = uow;
            _tenantGoogleOAuthSettingsService = tenantGoogleOAuthSettingsService;
            _googleTokenService = googleTokenService;
            _httpClientFactory = httpClientFactory;
            _userContextService = userContextService;
            _googleIntegrationLogService = googleIntegrationLogService;
            _logger = logger;
            _localizationService = localizationService;
        }

        public async Task<ApiResponse<GoogleCustomerMailSendResultDto>> SendCustomerMailAsync(
            long userId,
            SendGoogleCustomerMailDto dto,
            CancellationToken cancellationToken = default)
        {
            if (dto.CustomerId <= 0)
            {
                var msg = _localizationService.GetLocalizedString("GoogleGmailApiService.CustomerIdRequired");
                return ApiResponse<GoogleCustomerMailSendResultDto>.ErrorResult(msg, msg, StatusCodes.Status400BadRequest);
            }

            if (string.IsNullOrWhiteSpace(dto.Subject))
            {
                var msg = _localizationService.GetLocalizedString("GoogleGmailApiService.MailSubjectRequired");
                return ApiResponse<GoogleCustomerMailSendResultDto>.ErrorResult(msg, msg, StatusCodes.Status400BadRequest);
            }

            if (string.IsNullOrWhiteSpace(dto.Body))
            {
                var msg = _localizationService.GetLocalizedString("GoogleGmailApiService.MailBodyRequired");
                return ApiResponse<GoogleCustomerMailSendResultDto>.ErrorResult(msg, msg, StatusCodes.Status400BadRequest);
            }

            var tenantId = _userContextService.GetCurrentTenantId() ?? Guid.Empty;
            if (tenantId == Guid.Empty)
            {
                var msg = _localizationService.GetLocalizedString("OutlookEntegrationService.TenantContextMissing");
                return ApiResponse<GoogleCustomerMailSendResultDto>.ErrorResult(msg, msg, StatusCodes.Status400BadRequest);
            }

            var customer = await _uow.Customers.Query(tracking: false)
                .FirstOrDefaultAsync(x => x.Id == dto.CustomerId && !x.IsDeleted, cancellationToken).ConfigureAwait(false);
            if (customer == null)
            {
                var msg = _localizationService.GetLocalizedString("OutlookEntegrationService.CustomerNotFound");
                return ApiResponse<GoogleCustomerMailSendResultDto>.ErrorResult(msg, msg, StatusCodes.Status404NotFound);
            }

            Contact? contact = null;
            if (dto.ContactId.HasValue)
            {
                contact = await _uow.Contacts.Query(tracking: false)
                    .FirstOrDefaultAsync(x => x.Id == dto.ContactId.Value && !x.IsDeleted, cancellationToken).ConfigureAwait(false);

                if (contact == null)
                {
                    var msg = _localizationService.GetLocalizedString("OutlookEntegrationService.ContactNotFound");
                    return ApiResponse<GoogleCustomerMailSendResultDto>.ErrorResult(msg, msg, StatusCodes.Status404NotFound);
                }

                if (contact.CustomerId != customer.Id)
                {
                    var msg = _localizationService.GetLocalizedString("OutlookEntegrationService.ContactNotBelongToCustomer");
                    return ApiResponse<GoogleCustomerMailSendResultDto>.ErrorResult(msg, msg, StatusCodes.Status400BadRequest);
                }
            }

            var toRecipients = ParseEmails(dto.To);
            if (toRecipients.Count == 0)
            {
                if (!string.IsNullOrWhiteSpace(contact?.Email))
                {
                    toRecipients.Add(contact.Email.Trim());
                }
                else if (!string.IsNullOrWhiteSpace(customer.Email))
                {
                    toRecipients.Add(customer.Email.Trim());
                }
            }

            if (toRecipients.Count == 0)
            {
                return ApiResponse<GoogleCustomerMailSendResultDto>.ErrorResult(
                    _localizationService.GetLocalizedString("OutlookEntegrationService.RecipientEmailRequired"),
                    _localizationService.GetLocalizedString("OutlookEntegrationService.NoRecipientEmailFound"),
                    StatusCodes.Status400BadRequest);
            }

            var ccRecipients = ParseEmails(dto.Cc);
            var bccRecipients = ParseEmails(dto.Bcc);

            var oauthSettings = await _tenantGoogleOAuthSettingsService.GetRuntimeSettingsAsync(tenantId, cancellationToken).ConfigureAwait(false);
            if (oauthSettings == null || !oauthSettings.IsEnabled)
            {
                await WriteGoogleOperationalLogAsync(userId, tenantId, dto.CustomerId, false, "Warning", "google.gmail.send", "Google OAuth is not configured or disabled.", "oauth_disabled", cancellationToken).ConfigureAwait(false);
                var oauthMsg = _localizationService.GetLocalizedString("GoogleGmailApiService.OAuthNotConfigured");
                return ApiResponse<GoogleCustomerMailSendResultDto>.ErrorResult(oauthMsg, oauthMsg, StatusCodes.Status400BadRequest);
            }

            var account = await _googleTokenService.GetAccountAsync(userId, cancellationToken).ConfigureAwait(false);
            if (account == null || !account.IsConnected)
            {
                await WriteGoogleOperationalLogAsync(userId, tenantId, dto.CustomerId, false, "Warning", "google.gmail.send", "Google account is not connected.", "account_not_connected", cancellationToken).ConfigureAwait(false);
                var acctMsg = _localizationService.GetLocalizedString("GoogleGmailApiService.AccountNotConnected");
                return ApiResponse<GoogleCustomerMailSendResultDto>.ErrorResult(acctMsg, acctMsg, StatusCodes.Status400BadRequest);
            }

            var scopes = string.Join(' ', new[] { account.Scopes, oauthSettings.Scopes }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim()));
            if (!ScopeContains(scopes, GmailSendScope))
            {
                await WriteGoogleOperationalLogAsync(userId, tenantId, dto.CustomerId, false, "Warning", "google.gmail.send", "Google account does not have gmail.send scope.", "insufficient_scope", cancellationToken).ConfigureAwait(false);
                var scopeMsg = _localizationService.GetLocalizedString("GoogleGmailApiService.MailSendScopeMissing");
                return ApiResponse<GoogleCustomerMailSendResultDto>.ErrorResult(scopeMsg, scopeMsg, StatusCodes.Status400BadRequest);
            }

            var accessToken = await _googleTokenService.GetValidAccessTokenAsync(userId, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                await WriteGoogleOperationalLogAsync(userId, tenantId, dto.CustomerId, false, "Warning", "google.gmail.send", "Google token is invalid or expired.", "token_invalid", cancellationToken).ConfigureAwait(false);
                var tokenMsg = _localizationService.GetLocalizedString("GoogleGmailApiService.TokenInvalidOrExpired");
                return ApiResponse<GoogleCustomerMailSendResultDto>.ErrorResult(tokenMsg, tokenMsg, StatusCodes.Status400BadRequest);
            }

            var senderEmail = string.IsNullOrWhiteSpace(account.GoogleEmail) ? null : account.GoogleEmail!.Trim();
            var senderDisplayName = await ResolveSenderDisplayNameAsync(userId, cancellationToken).ConfigureAwait(false);
            var mimeRaw = BuildMimeRaw(senderDisplayName, senderEmail, toRecipients, ccRecipients, bccRecipients, dto.Subject.Trim(), dto.Body, dto.IsHtml);

            var gmailResponse = await SendViaGmailApiAsync(accessToken, mimeRaw, cancellationToken).ConfigureAwait(false);
            if (!gmailResponse.IsSuccess && IsInsufficientScopeError(gmailResponse))
            {
                var refreshedAccessToken = await _googleTokenService.GetValidAccessTokenAsync(
                    userId,
                    forceRefresh: true,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(refreshedAccessToken))
                {
                    gmailResponse = await SendViaGmailApiAsync(refreshedAccessToken, mimeRaw, cancellationToken).ConfigureAwait(false);
                }
            }
            if (!gmailResponse.IsSuccess)
            {
                try
                {
                    await WriteCustomerMailLogAsync(
                        tenantId,
                        userId,
                        customer,
                        contact,
                        senderEmail,
                        toRecipients,
                        ccRecipients,
                        bccRecipients,
                        dto,
                        isSuccess: false,
                        gmailResponse.ErrorCode,
                        gmailResponse.ErrorMessage,
                        googleMessageId: null,
                        googleThreadId: null,
                        sentAt: null,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                catch (DbUpdateException dbEx)
                {
                    _logger.LogError(dbEx, "Failed to persist failed Google mail log. CustomerId={CustomerId}, UserId={UserId}", dto.CustomerId, userId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error while persisting failed Google mail log. CustomerId={CustomerId}, UserId={UserId}", dto.CustomerId, userId);
                }

                await WriteGoogleOperationalLogAsync(
                    userId,
                    tenantId,
                    dto.CustomerId,
                    false,
                    "Error",
                    "google.gmail.send",
                    "Google Gmail API send failed.",
                    gmailResponse.ErrorCode,
                    cancellationToken,
                    new
                    {
                        customerId = dto.CustomerId,
                        to = string.Join("; ", toRecipients),
                        status = gmailResponse.ErrorCode
                    }).ConfigureAwait(false);

                if (IsInsufficientScopeError(gmailResponse))
                {
                    var insufMsg = _localizationService.GetLocalizedString("GoogleGmailApiService.InsufficientMailScope");
                    return ApiResponse<GoogleCustomerMailSendResultDto>.ErrorResult(
                        insufMsg,
                        gmailResponse.ErrorMessage ?? insufMsg,
                        StatusCodes.Status400BadRequest);
                }

                if (IsServiceDisabledError(gmailResponse, out var activationUrl))
                {
                    var message = _localizationService.GetLocalizedString("GoogleGmailApiService.GmailApiNotEnabled");
                    var detail = string.IsNullOrWhiteSpace(activationUrl)
                        ? (gmailResponse.ErrorMessage ?? "SERVICE_DISABLED")
                        : $"SERVICE_DISABLED. ActivationUrl: {activationUrl}";

                    return ApiResponse<GoogleCustomerMailSendResultDto>.ErrorResult(
                        message,
                        detail,
                        StatusCodes.Status400BadRequest);
                }

                var sendFailMsg = _localizationService.GetLocalizedString("GoogleGmailApiService.MailSendFailed");
                return ApiResponse<GoogleCustomerMailSendResultDto>.ErrorResult(
                    sendFailMsg,
                    gmailResponse.ErrorMessage ?? sendFailMsg,
                    StatusCodes.Status400BadRequest);
            }

            var sentAt = DateTimeOffset.UtcNow;
            GoogleCustomerMailLog successLog;
            try
            {
                successLog = await WriteCustomerMailLogAsync(
                    tenantId,
                    userId,
                    customer,
                    contact,
                    senderEmail,
                    toRecipients,
                    ccRecipients,
                    bccRecipients,
                    dto,
                    isSuccess: true,
                    errorCode: null,
                    errorMessage: null,
                    gmailResponse.MessageId,
                    gmailResponse.ThreadId,
                    sentAt,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Google mail sent but log save failed. CustomerId={CustomerId}, UserId={UserId}", dto.CustomerId, userId);
                var logFailMsg = _localizationService.GetLocalizedString("GoogleGmailApiService.MailSentButLogFailed");
                return ApiResponse<GoogleCustomerMailSendResultDto>.ErrorResult(
                    logFailMsg,
                    dbEx.InnerException?.Message ?? dbEx.Message,
                    StatusCodes.Status500InternalServerError);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Google mail sent but unexpected log save error occurred. CustomerId={CustomerId}, UserId={UserId}", dto.CustomerId, userId);
                var logFailMsg = _localizationService.GetLocalizedString("GoogleGmailApiService.MailSentButLogFailed");
                return ApiResponse<GoogleCustomerMailSendResultDto>.ErrorResult(
                    logFailMsg,
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }

            await WriteGoogleOperationalLogAsync(
                userId,
                tenantId,
                dto.CustomerId,
                true,
                "Info",
                "google.gmail.send",
                "Google Gmail API mail sent.",
                null,
                cancellationToken,
                new
                {
                    customerId = dto.CustomerId,
                    logId = successLog.Id,
                    messageId = gmailResponse.MessageId
                }).ConfigureAwait(false);

            return ApiResponse<GoogleCustomerMailSendResultDto>.SuccessResult(
                new GoogleCustomerMailSendResultDto
                {
                    LogId = successLog.Id,
                    IsSuccess = true,
                    GoogleMessageId = gmailResponse.MessageId,
                    GoogleThreadId = gmailResponse.ThreadId,
                    SentAt = sentAt
                },
                _localizationService.GetLocalizedString("GoogleGmailApiService.MailSentSuccessfully"));
        }

        public async Task<ApiResponse<PagedResponse<GoogleCustomerMailLogDto>>> GetCustomerMailLogsAsync(
            long userId,
            GoogleCustomerMailLogQueryDto query,
            CancellationToken cancellationToken = default)
        {
            var tenantId = _userContextService.GetCurrentTenantId() ?? Guid.Empty;
            if (tenantId == Guid.Empty)
            {
                var tenantMsg = _localizationService.GetLocalizedString("OutlookEntegrationService.TenantContextMissing");
                return ApiResponse<PagedResponse<GoogleCustomerMailLogDto>>.ErrorResult(tenantMsg, tenantMsg, StatusCodes.Status400BadRequest);
            }

            var pageNumber = query.PageNumber < 1 ? 1 : query.PageNumber;
            var pageSize = Math.Clamp(query.PageSize, 1, 100);

            IQueryable<GoogleCustomerMailLog> mailLogQuery = _uow.Repository<GoogleCustomerMailLog>()
                .Query()
                .Where(x => x.TenantId == tenantId)
                .Include(x => x.Customer)
                .Include(x => x.Contact)
                .Include(x => x.SentByUser);

            if (query.CustomerId.HasValue && query.CustomerId.Value > 0)
            {
                mailLogQuery = mailLogQuery.Where(x => x.CustomerId == query.CustomerId.Value);
            }

            if (query.ErrorsOnly)
            {
                mailLogQuery = mailLogQuery.Where(x => !x.IsSuccess);
            }

            mailLogQuery = mailLogQuery.ApplyFilters(query.Filters, query.FilterLogic, LogColumnMapping);

            var sortBy = string.IsNullOrWhiteSpace(query.SortBy) ? "createdDate" : query.SortBy;
            var sortDirection = string.IsNullOrWhiteSpace(query.SortDirection) ? "desc" : query.SortDirection;
            mailLogQuery = mailLogQuery.ApplySorting(sortBy, sortDirection, LogColumnMapping);

            var totalCount = await mailLogQuery.CountAsync(cancellationToken).ConfigureAwait(false);
            var items = await mailLogQuery
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new GoogleCustomerMailLogDto
                {
                    Id = x.Id,
                    CustomerId = x.CustomerId,
                    CustomerName = x.Customer.CustomerName,
                    ContactId = x.ContactId,
                    ContactName = x.Contact != null ? x.Contact.FullName : null,
                    SentByUserId = x.SentByUserId,
                    SentByUserName = x.SentByUser != null ? x.SentByUser.FullName : null,
                    Provider = x.Provider,
                    SenderEmail = x.SenderEmail,
                    ToEmails = x.ToEmails,
                    CcEmails = x.CcEmails,
                    BccEmails = x.BccEmails,
                    Subject = x.Subject,
                    Body = x.Body,
                    BodyPreview = BuildPreview(x.Body, 300),
                    IsHtml = x.IsHtml,
                    TemplateKey = x.TemplateKey,
                    TemplateName = x.TemplateName,
                    TemplateVersion = x.TemplateVersion,
                    IsSuccess = x.IsSuccess,
                    ErrorCode = x.ErrorCode,
                    ErrorMessage = x.ErrorMessage,
                    GoogleMessageId = x.GoogleMessageId,
                    GoogleThreadId = x.GoogleThreadId,
                    SentAt = x.SentAt,
                    CreatedDate = x.CreatedDate
                })
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            return ApiResponse<PagedResponse<GoogleCustomerMailLogDto>>.SuccessResult(
                new PagedResponse<GoogleCustomerMailLogDto>
                {
                    Items = items,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                },
                "Customer mail logs retrieved.");
        }

        private async Task<GoogleCustomerMailLog> WriteCustomerMailLogAsync(
            Guid tenantId,
            long userId,
            Customer customer,
            Contact? contact,
            string? senderEmail,
            IReadOnlyList<string> toRecipients,
            IReadOnlyList<string> ccRecipients,
            IReadOnlyList<string> bccRecipients,
            SendGoogleCustomerMailDto dto,
            bool isSuccess,
            string? errorCode,
            string? errorMessage,
            string? googleMessageId,
            string? googleThreadId,
            DateTimeOffset? sentAt,
            CancellationToken cancellationToken)
        {
            var entity = new GoogleCustomerMailLog
            {
                TenantId = tenantId,
                CustomerId = customer.Id,
                ContactId = contact?.Id,
                SentByUserId = userId,
                Provider = "GoogleGmailApi",
                SenderEmail = TrimOrNull(senderEmail, 320),
                ToEmails = TrimOrNull(string.Join("; ", toRecipients), 4000) ?? string.Empty,
                CcEmails = TrimOrNull(string.Join("; ", ccRecipients), 4000),
                BccEmails = TrimOrNull(string.Join("; ", bccRecipients), 4000),
                Subject = TrimOrNull(dto.Subject, 512) ?? string.Empty,
                Body = dto.Body,
                IsHtml = dto.IsHtml,
                TemplateKey = TrimOrNull(dto.TemplateKey, 128),
                TemplateName = TrimOrNull(dto.TemplateName, 256),
                TemplateVersion = TrimOrNull(dto.TemplateVersion, 64),
                IsSuccess = isSuccess,
                ErrorCode = TrimOrNull(errorCode, 128),
                ErrorMessage = TrimOrNull(errorMessage, 2000),
                GoogleMessageId = TrimOrNull(googleMessageId, 512),
                GoogleThreadId = TrimOrNull(googleThreadId, 512),
                SentAt = sentAt,
                MetadataJson = JsonSerializer.Serialize(new
                {
                    customerName = customer.CustomerName,
                    contactName = contact?.FullName
                })
            };

            await _uow.Repository<GoogleCustomerMailLog>().AddAsync(entity).ConfigureAwait(false);
            await _uow.SaveChangesAsync().ConfigureAwait(false);
            return entity;
        }

        private async Task WriteGoogleOperationalLogAsync(
            long userId,
            Guid tenantId,
            long customerId,
            bool isSuccess,
            string severity,
            string operation,
            string message,
            string? errorCode,
            CancellationToken cancellationToken,
            object? metadata = null)
        {
            await _googleIntegrationLogService.WriteAsync(new GoogleIntegrationLogWriteDto
            {
                TenantId = tenantId,
                UserId = userId,
                ActivityId = null,
                IsSuccess = isSuccess,
                Severity = severity,
                Operation = operation,
                Provider = "GoogleGmailApi",
                Message = message,
                ErrorCode = errorCode,
                Metadata = metadata ?? new { customerId }
            }, cancellationToken).ConfigureAwait(false);
        }

        private async Task<string?> ResolveSenderDisplayNameAsync(long userId, CancellationToken cancellationToken)
        {
            var user = await _uow.Users.Query(tracking: false)
                .FirstOrDefaultAsync(x => x.Id == userId && !x.IsDeleted, cancellationToken).ConfigureAwait(false);

            if (user == null)
            {
                return null;
            }

            return string.IsNullOrWhiteSpace(user.FullName) ? user.Email : user.FullName;
        }

        private async Task<GmailSendResponse> SendViaGmailApiAsync(
            string accessToken,
            string rawMime,
            CancellationToken cancellationToken)
        {
            var payload = JsonSerializer.Serialize(new { raw = rawMime });
            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, GmailSendEndpoint)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return new GmailSendResponse
                {
                    IsSuccess = false,
                    ErrorCode = ((int)response.StatusCode).ToString(),
                    ErrorMessage = string.IsNullOrWhiteSpace(responseBody)
                        ? "Google Gmail API request failed."
                        : responseBody
                };
            }

            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                var messageId = doc.RootElement.TryGetProperty("id", out var idEl)
                    ? idEl.GetString()
                    : null;
                var threadId = doc.RootElement.TryGetProperty("threadId", out var threadEl)
                    ? threadEl.GetString()
                    : null;

                return new GmailSendResponse
                {
                    IsSuccess = true,
                    MessageId = messageId,
                    ThreadId = threadId
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse Gmail send response.");
                return new GmailSendResponse
                {
                    IsSuccess = false,
                    ErrorCode = "parse_error",
                    ErrorMessage = "Google Gmail response could not be parsed."
                };
            }
        }

        private static bool ScopeContains(string scopeList, string requiredScope)
        {
            if (string.IsNullOrWhiteSpace(scopeList) || string.IsNullOrWhiteSpace(requiredScope))
            {
                return false;
            }

            return scopeList
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(s => string.Equals(s, requiredScope, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsInsufficientScopeError(GmailSendResponse response)
        {
            if (response.IsSuccess)
            {
                return false;
            }

            var content = $"{response.ErrorCode} {response.ErrorMessage}";
            if (string.IsNullOrWhiteSpace(content))
            {
                return false;
            }

            return content.Contains("ACCESS_TOKEN_SCOPE_INSUFFICIENT", StringComparison.OrdinalIgnoreCase)
                || content.Contains("insufficientPermissions", StringComparison.OrdinalIgnoreCase)
                || content.Contains("insufficient authentication scopes", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsServiceDisabledError(GmailSendResponse response, out string? activationUrl)
        {
            activationUrl = null;
            if (response.IsSuccess)
            {
                return false;
            }

            var content = $"{response.ErrorCode} {response.ErrorMessage}";
            if (string.IsNullOrWhiteSpace(content))
            {
                return false;
            }

            var isDisabled = content.Contains("SERVICE_DISABLED", StringComparison.OrdinalIgnoreCase)
                || content.Contains("accessNotConfigured", StringComparison.OrdinalIgnoreCase)
                || content.Contains("Gmail API has not been used", StringComparison.OrdinalIgnoreCase);

            if (!isDisabled || string.IsNullOrWhiteSpace(response.ErrorMessage))
            {
                return isDisabled;
            }

            try
            {
                using var doc = JsonDocument.Parse(response.ErrorMessage);
                if (!doc.RootElement.TryGetProperty("error", out var errorEl)
                    || !errorEl.TryGetProperty("details", out var detailsEl)
                    || detailsEl.ValueKind != JsonValueKind.Array)
                {
                    return true;
                }

                foreach (var detail in detailsEl.EnumerateArray())
                {
                    if (detail.TryGetProperty("metadata", out var metadataEl)
                        && metadataEl.TryGetProperty("activationUrl", out var activationEl))
                    {
                        activationUrl = activationEl.GetString();
                        if (!string.IsNullOrWhiteSpace(activationUrl))
                        {
                            break;
                        }
                    }
                }
            }
            catch
            {
                // ignore parse errors, generic message is enough
            }

            return true;
        }

        private static List<string> ParseEmails(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new List<string>();
            }

            var pieces = raw
                .Split(new[] { ';', ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var valid = new List<string>();
            foreach (var piece in pieces)
            {
                try
                {
                    var mail = new MailAddress(piece);
                    valid.Add(mail.Address);
                }
                catch
                {
                    // Ignore invalid addresses to keep send flow resilient.
                }
            }

            return valid;
        }

        private static string BuildMimeRaw(
            string? senderDisplayName,
            string? senderEmail,
            IReadOnlyList<string> toRecipients,
            IReadOnlyList<string> ccRecipients,
            IReadOnlyList<string> bccRecipients,
            string subject,
            string body,
            bool isHtml)
        {
            var fromHeader = !string.IsNullOrWhiteSpace(senderEmail)
                ? (!string.IsNullOrWhiteSpace(senderDisplayName)
                    ? $"{senderDisplayName} <{senderEmail}>"
                    : senderEmail)
                : "me";

            var sb = new StringBuilder();
            sb.AppendLine($"From: {fromHeader}");
            sb.AppendLine($"To: {string.Join(", ", toRecipients)}");
            if (ccRecipients.Count > 0)
            {
                sb.AppendLine($"Cc: {string.Join(", ", ccRecipients)}");
            }

            if (bccRecipients.Count > 0)
            {
                sb.AppendLine($"Bcc: {string.Join(", ", bccRecipients)}");
            }

            sb.AppendLine($"Subject: {EncodeMimeHeader(subject)}");
            sb.AppendLine("MIME-Version: 1.0");
            sb.AppendLine($"Content-Type: {(isHtml ? "text/html" : "text/plain")}; charset=UTF-8");
            sb.AppendLine("Content-Transfer-Encoding: 8bit");
            sb.AppendLine();
            sb.Append(body ?? string.Empty);

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var base64 = Convert.ToBase64String(bytes);
            return base64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static string? BuildPreview(string? value, int maxLen)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var normalized = value.Trim();
            if (normalized.Length <= maxLen)
            {
                return normalized;
            }

            return normalized[..maxLen];
        }

        private static string? TrimOrNull(string? value, int maxLen)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            return trimmed.Length <= maxLen ? trimmed : trimmed[..maxLen];
        }

        private static string EncodeMimeHeader(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            // RFC 2047: encode non-ASCII header values as UTF-8 Base64.
            var bytes = Encoding.UTF8.GetBytes(value);
            var base64 = Convert.ToBase64String(bytes);
            return $"=?UTF-8?B?{base64}?=";
        }

        private sealed class GmailSendResponse
        {
            public bool IsSuccess { get; set; }
            public string? MessageId { get; set; }
            public string? ThreadId { get; set; }
            public string? ErrorCode { get; set; }
            public string? ErrorMessage { get; set; }
        }
    }
}
