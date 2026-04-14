using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCTVocabulary.Migrations
{
    /// <inheritdoc />
    public partial class AddUserWritingAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserWritingAttempts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    WritingExerciseId = table.Column<int>(type: "int", nullable: false),
                    WritingExerciseSentenceId = table.Column<int>(type: "int", nullable: false),
                    SubmittedAnswer = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Passed = table.Column<bool>(type: "bit", nullable: false),
                    UsedAi = table.Column<bool>(type: "bit", nullable: false),
                    EvaluationSource = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserWritingAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserWritingAttempts_Users",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserWritingAttempts_WritingExerciseSentences",
                        column: x => x.WritingExerciseSentenceId,
                        principalTable: "WritingExerciseSentences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserWritingAttempts_WritingExercises",
                        column: x => x.WritingExerciseId,
                        principalTable: "WritingExercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserWritingAttempts_UserId_WritingExerciseId_CreatedAtUtc",
                table: "UserWritingAttempts",
                columns: new[] { "UserID", "WritingExerciseId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserWritingAttempts_UserId_WritingExerciseSentenceId_CreatedAtUtc",
                table: "UserWritingAttempts",
                columns: new[] { "UserID", "WritingExerciseSentenceId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserWritingAttempts_WritingExerciseId",
                table: "UserWritingAttempts",
                column: "WritingExerciseId");

            migrationBuilder.CreateIndex(
                name: "IX_UserWritingAttempts_WritingExerciseSentenceId",
                table: "UserWritingAttempts",
                column: "WritingExerciseSentenceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserWritingAttempts");
        }
    }
}
