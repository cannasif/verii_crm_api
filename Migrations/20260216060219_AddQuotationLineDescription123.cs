using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace crm_api.Migrations
{
    /// <inheritdoc />
    public partial class AddQuotationLineDescription123 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Country_Name",
                table: "RII_COUNTRY");

            migrationBuilder.CreateIndex(
                name: "IX_Country_Name",
                table: "RII_COUNTRY",
                column: "Name",
                unique: true);
                
            migrationBuilder.AddColumn<string>(
                name: "Description1",
                table: "RII_QUOTATION_LINE",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description2",
                table: "RII_QUOTATION_LINE",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description3",
                table: "RII_QUOTATION_LINE",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Country_Name",
                table: "RII_COUNTRY");

            migrationBuilder.DropColumn(
                name: "Description1",
                table: "RII_QUOTATION_LINE");

            migrationBuilder.DropColumn(
                name: "Description2",
                table: "RII_QUOTATION_LINE");

            migrationBuilder.DropColumn(
                name: "Description3",
                table: "RII_QUOTATION_LINE");

            migrationBuilder.CreateIndex(
                name: "IX_Country_Name",
                table: "RII_COUNTRY",
                column: "Name");
        }
    }
}
