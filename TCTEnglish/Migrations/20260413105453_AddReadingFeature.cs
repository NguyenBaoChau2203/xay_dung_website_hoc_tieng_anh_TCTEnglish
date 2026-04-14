using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCTVocabulary.Migrations
{
    /// <inheritdoc />
    public partial class AddReadingFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReadingPassages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Level = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Topic = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadingPassages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReadingQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PassageId = table.Column<int>(type: "int", nullable: false),
                    QuestionText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadingQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReadingQuestions_ReadingPassages_PassageId",
                        column: x => x.PassageId,
                        principalTable: "ReadingPassages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserReadingHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ReadingPassageId = table.Column<int>(type: "int", nullable: false),
                    ViewedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserReadingHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserReadingHistories_ReadingPassages_ReadingPassageId",
                        column: x => x.ReadingPassageId,
                        principalTable: "ReadingPassages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserReadingHistories_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReadingOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuestionId = table.Column<int>(type: "int", nullable: false),
                    OptionText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsCorrect = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadingOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReadingOptions_ReadingQuestions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "ReadingQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReadingOptions_QuestionId",
                table: "ReadingOptions",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingQuestions_PassageId",
                table: "ReadingQuestions",
                column: "PassageId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReadingHistories_ReadingPassageId",
                table: "UserReadingHistories",
                column: "ReadingPassageId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReadingHistories_UserId_ReadingPassageId",
                table: "UserReadingHistories",
                columns: new[] { "UserId", "ReadingPassageId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReadingOptions");

            migrationBuilder.DropTable(
                name: "UserReadingHistories");

            migrationBuilder.DropTable(
                name: "ReadingQuestions");

            migrationBuilder.DropTable(
                name: "ReadingPassages");
        }
    }
}
