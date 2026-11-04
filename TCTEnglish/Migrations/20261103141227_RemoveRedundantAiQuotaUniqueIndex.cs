using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCTEnglish.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRedundantAiQuotaUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_AiUserDailyQuotas_UserId_DayUtc",
                table: "AiUserDailyQuotas");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "UX_AiUserDailyQuotas_UserId_DayUtc",
                table: "AiUserDailyQuotas",
                columns: new[] { "UserID", "DayUtc" },
                unique: true);
        }
    }
}
