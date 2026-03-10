using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace crm_api.Migrations
{
    /// <inheritdoc />
    public partial class AddOutlookMailAndOAuthImplementation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RII_OUTLOOK_CUSTOMER_MAIL_LOGS",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    ContactId = table.Column<long>(type: "bigint", nullable: true),
                    SentByUserId = table.Column<long>(type: "bigint", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SenderEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    ToEmails = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CcEmails = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    BccEmails = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsHtml = table.Column<bool>(type: "bit", nullable: false),
                    TemplateKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    TemplateName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    TemplateVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IsSuccess = table.Column<bool>(type: "bit", nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OutlookMessageId = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    OutlookConversationId = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    SentAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RII_OUTLOOK_CUSTOMER_MAIL_LOGS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RII_OUTLOOK_CUSTOMER_MAIL_LOGS_RII_CONTACT_ContactId",
                        column: x => x.ContactId,
                        principalTable: "RII_CONTACT",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RII_OUTLOOK_CUSTOMER_MAIL_LOGS_RII_CUSTOMER_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "RII_CUSTOMER",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RII_OUTLOOK_CUSTOMER_MAIL_LOGS_RII_USERS_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "RII_USERS",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RII_OUTLOOK_CUSTOMER_MAIL_LOGS_RII_USERS_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "RII_USERS",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RII_OUTLOOK_CUSTOMER_MAIL_LOGS_RII_USERS_SentByUserId",
                        column: x => x.SentByUserId,
                        principalTable: "RII_USERS",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RII_OUTLOOK_CUSTOMER_MAIL_LOGS_RII_USERS_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "RII_USERS",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RII_OUTLOOK_INTEGRATION_LOGS",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    Operation = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    IsSuccess = table.Column<bool>(type: "bit", nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ActivityId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ProviderEventId = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RII_OUTLOOK_INTEGRATION_LOGS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RII_OUTLOOK_INTEGRATION_LOGS_RII_USERS_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "RII_USERS",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RII_OUTLOOK_INTEGRATION_LOGS_RII_USERS_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "RII_USERS",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RII_OUTLOOK_INTEGRATION_LOGS_RII_USERS_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "RII_USERS",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RII_OUTLOOK_INTEGRATION_LOGS_RII_USERS_UserId",
                        column: x => x.UserId,
                        principalTable: "RII_USERS",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RII_USER_OUTLOOK_ACCOUNTS",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    OutlookEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RefreshTokenEncrypted = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    AccessTokenEncrypted = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Scopes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsConnected = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RII_USER_OUTLOOK_ACCOUNTS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RII_USER_OUTLOOK_ACCOUNTS_RII_USERS_UserId",
                        column: x => x.UserId,
                        principalTable: "RII_USERS",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutlookCustomerMailLogs_ContactId",
                table: "RII_OUTLOOK_CUSTOMER_MAIL_LOGS",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_OutlookCustomerMailLogs_CreatedDate",
                table: "RII_OUTLOOK_CUSTOMER_MAIL_LOGS",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_OutlookCustomerMailLogs_CustomerId",
                table: "RII_OUTLOOK_CUSTOMER_MAIL_LOGS",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_OutlookCustomerMailLogs_IsSuccess",
                table: "RII_OUTLOOK_CUSTOMER_MAIL_LOGS",
                column: "IsSuccess");

            migrationBuilder.CreateIndex(
                name: "IX_OutlookCustomerMailLogs_SentByUserId",
                table: "RII_OUTLOOK_CUSTOMER_MAIL_LOGS",
                column: "SentByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OutlookCustomerMailLogs_TenantId",
                table: "RII_OUTLOOK_CUSTOMER_MAIL_LOGS",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RII_OUTLOOK_CUSTOMER_MAIL_LOGS_CreatedBy",
                table: "RII_OUTLOOK_CUSTOMER_MAIL_LOGS",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_RII_OUTLOOK_CUSTOMER_MAIL_LOGS_DeletedBy",
                table: "RII_OUTLOOK_CUSTOMER_MAIL_LOGS",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_RII_OUTLOOK_CUSTOMER_MAIL_LOGS_UpdatedBy",
                table: "RII_OUTLOOK_CUSTOMER_MAIL_LOGS",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_OutlookIntegrationLogs_CreatedDate",
                table: "RII_OUTLOOK_INTEGRATION_LOGS",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_OutlookIntegrationLogs_TenantId",
                table: "RII_OUTLOOK_INTEGRATION_LOGS",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_OutlookIntegrationLogs_UserId",
                table: "RII_OUTLOOK_INTEGRATION_LOGS",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RII_OUTLOOK_INTEGRATION_LOGS_CreatedBy",
                table: "RII_OUTLOOK_INTEGRATION_LOGS",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_RII_OUTLOOK_INTEGRATION_LOGS_DeletedBy",
                table: "RII_OUTLOOK_INTEGRATION_LOGS",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_RII_OUTLOOK_INTEGRATION_LOGS_UpdatedBy",
                table: "RII_OUTLOOK_INTEGRATION_LOGS",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_UserOutlookAccounts_TenantId",
                table: "RII_USER_OUTLOOK_ACCOUNTS",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UserOutlookAccounts_UserId",
                table: "RII_USER_OUTLOOK_ACCOUNTS",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RII_OUTLOOK_CUSTOMER_MAIL_LOGS");

            migrationBuilder.DropTable(
                name: "RII_OUTLOOK_INTEGRATION_LOGS");

            migrationBuilder.DropTable(
                name: "RII_USER_OUTLOOK_ACCOUNTS");
        }
    }
}
