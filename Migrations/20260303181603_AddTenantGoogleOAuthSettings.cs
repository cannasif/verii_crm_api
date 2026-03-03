using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace crm_api.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantGoogleOAuthSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "RII_USER_GOOGLE_ACCOUNTS",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "RII_TENANT_GOOGLE_OAUTH_SETTINGS",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ClientSecretEncrypted = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    RedirectUri = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Scopes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RII_TENANT_GOOGLE_OAUTH_SETTINGS", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserGoogleAccounts_TenantId",
                table: "RII_USER_GOOGLE_ACCOUNTS",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantGoogleOAuthSettings_TenantId",
                table: "RII_TENANT_GOOGLE_OAUTH_SETTINGS",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RII_TENANT_GOOGLE_OAUTH_SETTINGS");

            migrationBuilder.DropIndex(
                name: "IX_UserGoogleAccounts_TenantId",
                table: "RII_USER_GOOGLE_ACCOUNTS");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "RII_USER_GOOGLE_ACCOUNTS");
        }
    }
}
