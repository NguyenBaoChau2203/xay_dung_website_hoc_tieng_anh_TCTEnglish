using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCTVocabulary.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentAdminAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentAdminActions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentOrderId = table.Column<long>(type: "bigint", nullable: true),
                    SubscriptionId = table.Column<long>(type: "bigint", nullable: true),
                    AdminUserId = table.Column<int>(type: "int", nullable: false),
                    ActionType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    OldStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NewStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAdminActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentAdminActions_PaymentOrders_PaymentOrderId",
                        column: x => x.PaymentOrderId,
                        principalTable: "PaymentOrders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PaymentAdminActions_UserSubscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "UserSubscriptions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PaymentAdminActions_Users_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAdminActions_AdminUserId",
                table: "PaymentAdminActions",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAdminActions_CreatedAtUtc",
                table: "PaymentAdminActions",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAdminActions_PaymentOrderId",
                table: "PaymentAdminActions",
                column: "PaymentOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAdminActions_SubscriptionId",
                table: "PaymentAdminActions",
                column: "SubscriptionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentAdminActions");
        }
    }
}
