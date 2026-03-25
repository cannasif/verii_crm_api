using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace crm_api.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityPaymentType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "RII_ACTIVITY",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ActivityMeetingTypeId",
                table: "RII_ACTIVITY",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ActivityShippingId",
                table: "RII_ACTIVITY",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ActivityTopicPurposeId",
                table: "RII_ACTIVITY",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PaymentTypeId",
                table: "RII_ACTIVITY",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RII_ACTIVITY_MEETING_TYPE",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RII_ACTIVITY_MEETING_TYPE", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RII_ACTIVITY_MEETING_TYPE_RII_USERS_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "RII_USERS",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RII_ACTIVITY_MEETING_TYPE_RII_USERS_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "RII_USERS",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RII_ACTIVITY_MEETING_TYPE_RII_USERS_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "RII_USERS",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RII_ACTIVITY_SHIPPING",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RII_ACTIVITY_SHIPPING", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RII_ACTIVITY_SHIPPING_RII_USERS_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "RII_USERS",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RII_ACTIVITY_SHIPPING_RII_USERS_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "RII_USERS",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RII_ACTIVITY_SHIPPING_RII_USERS_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "RII_USERS",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RII_ACTIVITY_TOPIC_PURPOSE",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RII_ACTIVITY_TOPIC_PURPOSE", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RII_ACTIVITY_TOPIC_PURPOSE_RII_USERS_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "RII_USERS",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RII_ACTIVITY_TOPIC_PURPOSE_RII_USERS_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "RII_USERS",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RII_ACTIVITY_TOPIC_PURPOSE_RII_USERS_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "RII_USERS",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Activity_ActivityMeetingTypeId",
                table: "RII_ACTIVITY",
                column: "ActivityMeetingTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Activity_ActivityShippingId",
                table: "RII_ACTIVITY",
                column: "ActivityShippingId");

            migrationBuilder.CreateIndex(
                name: "IX_Activity_ActivityTopicPurposeId",
                table: "RII_ACTIVITY",
                column: "ActivityTopicPurposeId");

            migrationBuilder.CreateIndex(
                name: "IX_Activity_PaymentTypeId",
                table: "RII_ACTIVITY",
                column: "PaymentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityMeetingType_CreatedDate",
                table: "RII_ACTIVITY_MEETING_TYPE",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityMeetingType_IsDeleted",
                table: "RII_ACTIVITY_MEETING_TYPE",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityMeetingType_Name",
                table: "RII_ACTIVITY_MEETING_TYPE",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_RII_ACTIVITY_MEETING_TYPE_CreatedBy",
                table: "RII_ACTIVITY_MEETING_TYPE",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_RII_ACTIVITY_MEETING_TYPE_DeletedBy",
                table: "RII_ACTIVITY_MEETING_TYPE",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_RII_ACTIVITY_MEETING_TYPE_UpdatedBy",
                table: "RII_ACTIVITY_MEETING_TYPE",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityShipping_CreatedDate",
                table: "RII_ACTIVITY_SHIPPING",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityShipping_IsDeleted",
                table: "RII_ACTIVITY_SHIPPING",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityShipping_Name",
                table: "RII_ACTIVITY_SHIPPING",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_RII_ACTIVITY_SHIPPING_CreatedBy",
                table: "RII_ACTIVITY_SHIPPING",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_RII_ACTIVITY_SHIPPING_DeletedBy",
                table: "RII_ACTIVITY_SHIPPING",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_RII_ACTIVITY_SHIPPING_UpdatedBy",
                table: "RII_ACTIVITY_SHIPPING",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityTopicPurpose_CreatedDate",
                table: "RII_ACTIVITY_TOPIC_PURPOSE",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityTopicPurpose_IsDeleted",
                table: "RII_ACTIVITY_TOPIC_PURPOSE",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityTopicPurpose_Name",
                table: "RII_ACTIVITY_TOPIC_PURPOSE",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_RII_ACTIVITY_TOPIC_PURPOSE_CreatedBy",
                table: "RII_ACTIVITY_TOPIC_PURPOSE",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_RII_ACTIVITY_TOPIC_PURPOSE_DeletedBy",
                table: "RII_ACTIVITY_TOPIC_PURPOSE",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_RII_ACTIVITY_TOPIC_PURPOSE_UpdatedBy",
                table: "RII_ACTIVITY_TOPIC_PURPOSE",
                column: "UpdatedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_RII_ACTIVITY_RII_ACTIVITY_MEETING_TYPE_ActivityMeetingTypeId",
                table: "RII_ACTIVITY",
                column: "ActivityMeetingTypeId",
                principalTable: "RII_ACTIVITY_MEETING_TYPE",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_RII_ACTIVITY_RII_ACTIVITY_SHIPPING_ActivityShippingId",
                table: "RII_ACTIVITY",
                column: "ActivityShippingId",
                principalTable: "RII_ACTIVITY_SHIPPING",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_RII_ACTIVITY_RII_ACTIVITY_TOPIC_PURPOSE_ActivityTopicPurposeId",
                table: "RII_ACTIVITY",
                column: "ActivityTopicPurposeId",
                principalTable: "RII_ACTIVITY_TOPIC_PURPOSE",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_RII_ACTIVITY_RII_PAYMENT_TYPE_PaymentTypeId",
                table: "RII_ACTIVITY",
                column: "PaymentTypeId",
                principalTable: "RII_PAYMENT_TYPE",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RII_ACTIVITY_RII_ACTIVITY_MEETING_TYPE_ActivityMeetingTypeId",
                table: "RII_ACTIVITY");

            migrationBuilder.DropForeignKey(
                name: "FK_RII_ACTIVITY_RII_ACTIVITY_SHIPPING_ActivityShippingId",
                table: "RII_ACTIVITY");

            migrationBuilder.DropForeignKey(
                name: "FK_RII_ACTIVITY_RII_ACTIVITY_TOPIC_PURPOSE_ActivityTopicPurposeId",
                table: "RII_ACTIVITY");

            migrationBuilder.DropForeignKey(
                name: "FK_RII_ACTIVITY_RII_PAYMENT_TYPE_PaymentTypeId",
                table: "RII_ACTIVITY");

            migrationBuilder.DropTable(
                name: "RII_ACTIVITY_MEETING_TYPE");

            migrationBuilder.DropTable(
                name: "RII_ACTIVITY_SHIPPING");

            migrationBuilder.DropTable(
                name: "RII_ACTIVITY_TOPIC_PURPOSE");

            migrationBuilder.DropIndex(
                name: "IX_Activity_ActivityMeetingTypeId",
                table: "RII_ACTIVITY");

            migrationBuilder.DropIndex(
                name: "IX_Activity_ActivityShippingId",
                table: "RII_ACTIVITY");

            migrationBuilder.DropIndex(
                name: "IX_Activity_ActivityTopicPurposeId",
                table: "RII_ACTIVITY");

            migrationBuilder.DropIndex(
                name: "IX_Activity_PaymentTypeId",
                table: "RII_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "ActivityMeetingTypeId",
                table: "RII_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "ActivityShippingId",
                table: "RII_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "ActivityTopicPurposeId",
                table: "RII_ACTIVITY");

            migrationBuilder.DropColumn(
                name: "PaymentTypeId",
                table: "RII_ACTIVITY");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "RII_ACTIVITY",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);
        }
    }
}
