using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCTVocabulary.Migrations
{
    /// <inheritdoc />
    public partial class PaymentCoreHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PaymentEvents_Provider_EventKey",
                table: "PaymentEvents");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "PaymentOrders",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<DateTime>(
                name: "ActivatedAtUtc",
                table: "PaymentOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankCode",
                table: "PaymentOrders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankTransactionNo",
                table: "PaymentOrders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CardType",
                table: "PaymentOrders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmedAtUtc",
                table: "PaymentOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConfirmedByUserId",
                table: "PaymentOrders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayType",
                table: "PaymentOrders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RawStatus",
                table: "PaymentOrders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "PaymentOrders",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<string>(
                name: "EventKey",
                table: "PaymentEvents",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<string>(
                name: "EventType",
                table: "PaymentEvents",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ResultCode",
                table: "PaymentEvents",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            // Backfill EventType for all existing PaymentEvent rows before
            // creating the new unique index. Existing rows were all IPN events.
            migrationBuilder.Sql(
                "UPDATE PaymentEvents SET EventType = 'ipn' WHERE EventType = '' OR EventType IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEvents_Provider_EventType_EventKey",
                table: "PaymentEvents",
                columns: new[] { "Provider", "EventType", "EventKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PaymentEvents_Provider_EventType_EventKey",
                table: "PaymentEvents");

            migrationBuilder.DropColumn(
                name: "ActivatedAtUtc",
                table: "PaymentOrders");

            migrationBuilder.DropColumn(
                name: "BankCode",
                table: "PaymentOrders");

            migrationBuilder.DropColumn(
                name: "BankTransactionNo",
                table: "PaymentOrders");

            migrationBuilder.DropColumn(
                name: "CardType",
                table: "PaymentOrders");

            migrationBuilder.DropColumn(
                name: "ConfirmedAtUtc",
                table: "PaymentOrders");

            migrationBuilder.DropColumn(
                name: "ConfirmedByUserId",
                table: "PaymentOrders");

            migrationBuilder.DropColumn(
                name: "PayType",
                table: "PaymentOrders");

            migrationBuilder.DropColumn(
                name: "RawStatus",
                table: "PaymentOrders");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "PaymentOrders");

            migrationBuilder.DropColumn(
                name: "EventType",
                table: "PaymentEvents");

            migrationBuilder.DropColumn(
                name: "ResultCode",
                table: "PaymentEvents");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "PaymentOrders",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "EventKey",
                table: "PaymentEvents",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEvents_Provider_EventKey",
                table: "PaymentEvents",
                columns: new[] { "Provider", "EventKey" },
                unique: true);
        }
    }
}
