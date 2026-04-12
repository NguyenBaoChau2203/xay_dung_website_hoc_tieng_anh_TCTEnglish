using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCTVocabulary.Migrations
{
    /// <inheritdoc />
    public partial class AddPremiumWritingOwnershipAndGenerationLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WritingExercises_Published_Level_ContentType_Topic",
                table: "WritingExercises");

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "WritingExercises",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "admin");

            migrationBuilder.AddColumn<int>(
                name: "UserID",
                table: "WritingExercises",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WritingGenerationLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    RequestType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsSuccess = table.Column<bool>(type: "bit", nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WritingGenerationLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WritingGenerationLogs_Users",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_WritingExercises_UserId_IsPublished_CreatedAt",
                table: "WritingExercises",
                columns: new[] { "UserID", "IsPublished", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WritingExercises_UserId_IsPublished_Level_ContentType_Topic",
                table: "WritingExercises",
                columns: new[] { "UserID", "IsPublished", "Level", "ContentType", "Topic" });

            migrationBuilder.CreateIndex(
                name: "IX_WritingGenerationLogs_UserId_RequestedAtUtc",
                table: "WritingGenerationLogs",
                columns: new[] { "UserID", "RequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WritingGenerationLogs_UserId_RequestType_RequestedAtUtc",
                table: "WritingGenerationLogs",
                columns: new[] { "UserID", "RequestType", "RequestedAtUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_WritingExercises_Users",
                table: "WritingExercises",
                column: "UserID",
                principalTable: "Users",
                principalColumn: "UserID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WritingExercises_Users",
                table: "WritingExercises");

            migrationBuilder.DropTable(
                name: "WritingGenerationLogs");

            migrationBuilder.DropIndex(
                name: "IX_WritingExercises_UserId_IsPublished_CreatedAt",
                table: "WritingExercises");

            migrationBuilder.DropIndex(
                name: "IX_WritingExercises_UserId_IsPublished_Level_ContentType_Topic",
                table: "WritingExercises");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "WritingExercises");

            migrationBuilder.DropColumn(
                name: "UserID",
                table: "WritingExercises");

            migrationBuilder.CreateIndex(
                name: "IX_WritingExercises_Published_Level_ContentType_Topic",
                table: "WritingExercises",
                columns: new[] { "IsPublished", "Level", "ContentType", "Topic" });
        }
    }
}
