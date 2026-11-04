using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCTEnglish.Migrations
{
    /// <inheritdoc />
    public partial class AddAiUserDailyQuota : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiUserDailyQuotas",
                columns: table => new
                {
                    UserID = table.Column<int>(type: "int", nullable: false),
                    DayUtc = table.Column<DateTime>(type: "date", nullable: false),
                    PendingQuestions = table.Column<int>(type: "int", nullable: false),
                    SettledQuestions = table.Column<int>(type: "int", nullable: false),
                    PendingTokens = table.Column<int>(type: "int", nullable: false),
                    SettledTokens = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "(UTC_TIMESTAMP(6))")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiUserDailyQuotas", x => new { x.UserID, x.DayUtc });
                    table.ForeignKey(
                        name: "FK_AiUserDailyQuotas_Users",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "UX_AiUserDailyQuotas_UserId_DayUtc",
                table: "AiUserDailyQuotas",
                columns: new[] { "UserID", "DayUtc" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiUserDailyQuotas");
        }
    }
}
