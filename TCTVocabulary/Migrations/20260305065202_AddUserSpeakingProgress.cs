using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCTVocabulary.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSpeakingProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserSpeakingProgresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    SentenceId = table.Column<int>(type: "int", nullable: false),
                    TotalScore = table.Column<double>(type: "float", nullable: false),
                    AccuracyScore = table.Column<double>(type: "float", nullable: false),
                    FluencyScore = table.Column<double>(type: "float", nullable: false),
                    CompletenessScore = table.Column<double>(type: "float", nullable: false),
                    PracticedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSpeakingProgresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSpeakingProgress_SpeakingSentences",
                        column: x => x.SentenceId,
                        principalTable: "SpeakingSentences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserSpeakingProgress_Users",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserSpeakingProgresses_SentenceId",
                table: "UserSpeakingProgresses",
                column: "SentenceId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSpeakingProgresses_UserId",
                table: "UserSpeakingProgresses",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserSpeakingProgresses");
        }
    }
}
