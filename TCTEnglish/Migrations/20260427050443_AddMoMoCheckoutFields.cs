using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCTVocabulary.Migrations
{
    /// <inheritdoc />
    public partial class AddMoMoCheckoutFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProviderDeepLink",
                table: "PaymentOrders",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderPaymentUrl",
                table: "PaymentOrders",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderQrCodePayload",
                table: "PaymentOrders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderRequestId",
                table: "PaymentOrders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProviderDeepLink",
                table: "PaymentOrders");

            migrationBuilder.DropColumn(
                name: "ProviderPaymentUrl",
                table: "PaymentOrders");

            migrationBuilder.DropColumn(
                name: "ProviderQrCodePayload",
                table: "PaymentOrders");

            migrationBuilder.DropColumn(
                name: "ProviderRequestId",
                table: "PaymentOrders");
        }
    }
}
