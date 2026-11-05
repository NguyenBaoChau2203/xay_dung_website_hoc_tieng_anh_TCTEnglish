using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCTEnglish.Migrations
{
    /// <inheritdoc />
    public partial class AddWritingAcceptedAnswers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WritingAcceptedAnswers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    WritingExerciseSentenceId = table.Column<int>(type: "int", nullable: false),
                    AnswerText = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NormalizedHash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WritingAcceptedAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WritingAcceptedAnswers_Users",
                        column: x => x.ApprovedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WritingAcceptedAnswers_WritingExerciseSentences",
                        column: x => x.WritingExerciseSentenceId,
                        principalTable: "WritingExerciseSentences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_WritingAcceptedAnswers_ApprovedByUserId",
                table: "WritingAcceptedAnswers",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WritingAcceptedAnswers_WritingExerciseSentenceId",
                table: "WritingAcceptedAnswers",
                column: "WritingExerciseSentenceId");

            migrationBuilder.CreateIndex(
                name: "UX_WritingAcceptedAnswers_Sentence_Hash",
                table: "WritingAcceptedAnswers",
                columns: new[] { "WritingExerciseSentenceId", "NormalizedHash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WritingAcceptedAnswers");
        }
    }
}
