using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace crm_api.Migrations
{
    /// <inheritdoc />
    public partial class AddQuotationLinkColumnsToTempQuotation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "QuotationId",
                table: "RII_TEMP_QUOTATTION",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuotationNo",
                table: "RII_TEMP_QUOTATTION",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RevisionId",
                table: "RII_TEMP_QUOTATTION",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RII_TEMP_QUOTATTION_QuotationId",
                table: "RII_TEMP_QUOTATTION",
                column: "QuotationId");

            migrationBuilder.CreateIndex(
                name: "IX_RII_TEMP_QUOTATTION_RevisionId",
                table: "RII_TEMP_QUOTATTION",
                column: "RevisionId");

            migrationBuilder.AddForeignKey(
                name: "FK_RII_TEMP_QUOTATTION_RII_QUOTATION_QuotationId",
                table: "RII_TEMP_QUOTATTION",
                column: "QuotationId",
                principalTable: "RII_QUOTATION",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_RII_TEMP_QUOTATTION_RII_TEMP_QUOTATTION_RevisionId",
                table: "RII_TEMP_QUOTATTION",
                column: "RevisionId",
                principalTable: "RII_TEMP_QUOTATTION",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RII_TEMP_QUOTATTION_RII_QUOTATION_QuotationId",
                table: "RII_TEMP_QUOTATTION");

            migrationBuilder.DropForeignKey(
                name: "FK_RII_TEMP_QUOTATTION_RII_TEMP_QUOTATTION_RevisionId",
                table: "RII_TEMP_QUOTATTION");

            migrationBuilder.DropIndex(
                name: "IX_RII_TEMP_QUOTATTION_QuotationId",
                table: "RII_TEMP_QUOTATTION");

            migrationBuilder.DropIndex(
                name: "IX_RII_TEMP_QUOTATTION_RevisionId",
                table: "RII_TEMP_QUOTATTION");

            migrationBuilder.DropColumn(
                name: "QuotationId",
                table: "RII_TEMP_QUOTATTION");

            migrationBuilder.DropColumn(
                name: "QuotationNo",
                table: "RII_TEMP_QUOTATTION");

            migrationBuilder.DropColumn(
                name: "RevisionId",
                table: "RII_TEMP_QUOTATTION");
        }
    }
}
