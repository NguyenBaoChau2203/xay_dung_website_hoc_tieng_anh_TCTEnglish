using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCTEnglish.Migrations
{
    /// <inheritdoc />
    public partial class AddReadingUserTranslation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS ReadingTranslationVotes;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS ReadingUserTranslations;");
            migrationBuilder.CreateTable(
                name: "ReadingUserTranslations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ReadingPassageId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    TranslatedTitle = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TranslatedContent = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AiScore = table.Column<int>(type: "int", nullable: true),
                    AiFeedback = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsAiApproved = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    IsPublic = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    LikeCount = table.Column<int>(type: "int", nullable: false),
                    DislikeCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadingUserTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReadingUserTranslations_ReadingPassages_ReadingPassageId",
                        column: x => x.ReadingPassageId,
                        principalTable: "ReadingPassages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReadingUserTranslations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ReadingTranslationVotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    TranslationId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    VoteType = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadingTranslationVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReadingTranslationVotes_ReadingUserTranslations_TranslationId",
                        column: x => x.TranslationId,
                        principalTable: "ReadingUserTranslations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReadingTranslationVotes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserID");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingTranslationVotes_TranslationId_UserId",
                table: "ReadingTranslationVotes",
                columns: new[] { "TranslationId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReadingTranslationVotes_UserId",
                table: "ReadingTranslationVotes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingUserTranslations_ReadingPassageId",
                table: "ReadingUserTranslations",
                column: "ReadingPassageId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingUserTranslations_UserId_ReadingPassageId",
                table: "ReadingUserTranslations",
                columns: new[] { "UserId", "ReadingPassageId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReadingTranslationVotes");

            migrationBuilder.DropTable(
                name: "ReadingUserTranslations");
        }
    }
}
