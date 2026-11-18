using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCTEnglish.Migrations
{
    /// <inheritdoc />
    public partial class AddUserWritingExerciseVisits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserWritingExerciseVisits",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    WritingExerciseId = table.Column<int>(type: "int", nullable: false),
                    LastOpenedAtUtc = table.Column<DateTime>(type: "datetime(6)", precision: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserWritingExerciseVisits", x => new { x.UserId, x.WritingExerciseId });
                    table.ForeignKey(
                        name: "FK_UserWritingExerciseVisits_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserWritingExerciseVisits_WritingExercises_WritingExerciseId",
                        column: x => x.WritingExerciseId,
                        principalTable: "WritingExercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_UserWritingExerciseVisits_UserId_Opened_Exercise",
                table: "UserWritingExerciseVisits",
                columns: new[] { "UserId", "LastOpenedAtUtc", "WritingExerciseId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserWritingExerciseVisits_WritingExerciseId",
                table: "UserWritingExerciseVisits",
                column: "WritingExerciseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserWritingExerciseVisits");
        }
    }
}
