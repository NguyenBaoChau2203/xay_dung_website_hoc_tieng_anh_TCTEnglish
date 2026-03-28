using System;
using Microsoft.EntityFrameworkCore.Migrations;
using TCTVocabulary.Models;

#nullable disable

namespace TCTVocabulary.Migrations
{
    /// <inheritdoc />
    public partial class AddWritingExerciseTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WritingExercises",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Level = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Topic = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PreviewText = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WritingExercises", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WritingExerciseSentences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WritingExerciseId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    VietnameseText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EnglishMeaning = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BreakAfter = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WritingExerciseSentences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WritingExerciseSentences_WritingExercises",
                        column: x => x.WritingExerciseId,
                        principalTable: "WritingExercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WritingExercises_Published_Level_ContentType_Topic",
                table: "WritingExercises",
                columns: new[] { "IsPublished", "Level", "ContentType", "Topic" });

            migrationBuilder.CreateIndex(
                name: "IX_WritingExerciseSentences_WritingExerciseId_SortOrder",
                table: "WritingExerciseSentences",
                columns: new[] { "WritingExerciseId", "SortOrder" },
                unique: true);

            migrationBuilder.InsertData(
                table: "WritingExercises",
                columns: new[] { "Id", "Title", "Level", "ContentType", "Topic", "PreviewText", "IsPublished", "CreatedAt" },
                values: WritingExerciseSeedData.GetExerciseRows());

            migrationBuilder.InsertData(
                table: "WritingExerciseSentences",
                columns: new[] { "Id", "WritingExerciseId", "SortOrder", "VietnameseText", "EnglishMeaning", "BreakAfter" },
                values: WritingExerciseSeedData.GetSentenceRows());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WritingExerciseSentences");

            migrationBuilder.DropTable(
                name: "WritingExercises");
        }
    }
}
