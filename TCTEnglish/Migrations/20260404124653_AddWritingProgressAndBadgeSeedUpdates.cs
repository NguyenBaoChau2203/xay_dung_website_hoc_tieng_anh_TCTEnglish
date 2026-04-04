using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TCTVocabulary.Migrations
{
    /// <inheritdoc />
    public partial class AddWritingProgressAndBadgeSeedUpdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StreakXpAwarded",
                table: "UserDailyActivities",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "UserWritingExerciseProgresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    WritingExerciseID = table.Column<int>(type: "int", nullable: false),
                    TotalSentenceCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    PassedSentenceCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    AttemptCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    LastAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserWritingExerciseProgresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserWritingExerciseProgresses_Users",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserWritingExerciseProgresses_WritingExercises",
                        column: x => x.WritingExerciseID,
                        principalTable: "WritingExercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserWritingSentenceProgresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    WritingExerciseID = table.Column<int>(type: "int", nullable: false),
                    SentenceID = table.Column<int>(type: "int", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsPassed = table.Column<bool>(type: "bit", nullable: false),
                    AcceptedAnswer = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LastAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PassedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserWritingSentenceProgresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserWritingSentenceProgresses_Users",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserWritingSentenceProgresses_WritingExerciseSentences",
                        column: x => x.SentenceID,
                        principalTable: "WritingExerciseSentences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserWritingSentenceProgresses_WritingExercises",
                        column: x => x.WritingExerciseID,
                        principalTable: "WritingExercises",
                        principalColumn: "Id");
                });

            migrationBuilder.InsertData(
                table: "Badges",
                columns: new[] { "Id", "Code", "Description", "IconClass", "MetricType", "Name", "SortOrder", "ThresholdValue" },
                values: new object[,]
                {
                    { 9, "vocabulary-first-mastered", "Hoàn thành 1 thẻ Vocabulary ở trạng thái Mastered.", "fas fa-book-open", 6, "Mở khóa Từ vựng", 9, 1 },
                    { 10, "vocabulary-ten-mastered", "Hoàn thành 10 thẻ Vocabulary ở trạng thái Mastered.", "fas fa-spell-check", 6, "Nhịp từ vựng", 10, 10 },
                    { 11, "writing-first-exercise", "Hoàn thành bài Writing đầu tiên.", "fas fa-pen", 7, "Mở khóa Writing", 11, 1 },
                    { 12, "writing-five-exercises", "Hoàn thành 5 bài Writing để duy trì thói quen luyện viết.", "fas fa-pen-fancy", 7, "Viết chắc tay", 12, 5 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserWritingExerciseProgresses_UserId_WritingExerciseId",
                table: "UserWritingExerciseProgresses",
                columns: new[] { "UserID", "WritingExerciseID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserWritingExerciseProgresses_WritingExerciseID",
                table: "UserWritingExerciseProgresses",
                column: "WritingExerciseID");

            migrationBuilder.CreateIndex(
                name: "IX_UserWritingSentenceProgresses_SentenceID",
                table: "UserWritingSentenceProgresses",
                column: "SentenceID");

            migrationBuilder.CreateIndex(
                name: "IX_UserWritingSentenceProgresses_UserId_SentenceId",
                table: "UserWritingSentenceProgresses",
                columns: new[] { "UserID", "SentenceID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserWritingSentenceProgresses_WritingExerciseID",
                table: "UserWritingSentenceProgresses",
                column: "WritingExerciseID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserWritingExerciseProgresses");

            migrationBuilder.DropTable(
                name: "UserWritingSentenceProgresses");

            migrationBuilder.DeleteData(
                table: "Badges",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "Badges",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "Badges",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "Badges",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DropColumn(
                name: "StreakXpAwarded",
                table: "UserDailyActivities");
        }
    }
}
