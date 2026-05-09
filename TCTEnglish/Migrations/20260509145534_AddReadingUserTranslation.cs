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
            migrationBuilder.CreateTable(
                name: "ReadingUserTranslations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ReadingPassageId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    TranslatedTitle = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TranslatedContent = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AiScore = table.Column<int>(type: "int", nullable: true),
                    AiFeedback = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsAiApproved = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    IsPublic = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    LikeCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    DislikeCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "(UTC_TIMESTAMP(6))"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "(UTC_TIMESTAMP(6))")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadingUserTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReadingUserTranslations_ReadingPassages",
                        column: x => x.ReadingPassageId,
                        principalTable: "ReadingPassages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReadingUserTranslations_Users",
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
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "(UTC_TIMESTAMP(6))")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadingTranslationVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReadingTranslationVotes_ReadingUserTranslations",
                        column: x => x.TranslationId,
                        principalTable: "ReadingUserTranslations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReadingTranslationVotes_Users",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
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
                name: "IX_ReadingUserTranslations_PassageId_UserId",
                table: "ReadingUserTranslations",
                columns: new[] { "ReadingPassageId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReadingUserTranslations_UserId",
                table: "ReadingUserTranslations",
                column: "UserId");
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
