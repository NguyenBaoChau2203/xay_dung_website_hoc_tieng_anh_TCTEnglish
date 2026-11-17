using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCTEnglish.Migrations
{
    /// <inheritdoc />
    public partial class AddWritingGenerationOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FallbackReason",
                table: "WritingExercises",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "GenerationMethod",
                table: "WritingExercises",
                type: "varchar(30)",
                maxLength: 30,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "InputMode",
                table: "WritingExercises",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TemplateKey",
                table: "WritingExercises",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "TemplateVersion",
                table: "WritingExercises",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WritingGenerationOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PayloadHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    InputMode = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Level = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ContentType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Length = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Tone = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    State = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Fence = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LeaseExpiresAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    OfferExpiresAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    QuotaDayUtc = table.Column<DateTime>(type: "date", nullable: false),
                    Reserved = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    QuotaSettled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CandidateKeys = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CatalogVersion = table.Column<int>(type: "int", nullable: false),
                    ErrorCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ConfirmationHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GenerationMethod = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TemplateKey = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TemplateVersion = table.Column<int>(type: "int", nullable: true),
                    ExerciseId = table.Column<int>(type: "int", nullable: true),
                    SentenceCount = table.Column<int>(type: "int", nullable: false),
                    KnownProviderTokens = table.Column<int>(type: "int", nullable: false),
                    UncertainProviderTokenUpper = table.Column<int>(type: "int", nullable: false),
                    ProviderOutcomeUncertain = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WritingGenerationOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WritingGenerationOperations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WritingGenerationOperations_WritingExercises_ExerciseId",
                        column: x => x.ExerciseId,
                        principalTable: "WritingExercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_WritingGenerationOperations_ExerciseId",
                table: "WritingGenerationOperations",
                column: "ExerciseId");

            migrationBuilder.CreateIndex(
                name: "IX_WritingGenerationOperations_UserId_IdempotencyKey",
                table: "WritingGenerationOperations",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WritingGenerationOperations_UserId_Reserved_LeaseExpiresAtUtc",
                table: "WritingGenerationOperations",
                columns: new[] { "UserId", "Reserved", "LeaseExpiresAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WritingGenerationOperations");

            migrationBuilder.DropColumn(
                name: "FallbackReason",
                table: "WritingExercises");

            migrationBuilder.DropColumn(
                name: "GenerationMethod",
                table: "WritingExercises");

            migrationBuilder.DropColumn(
                name: "InputMode",
                table: "WritingExercises");

            migrationBuilder.DropColumn(
                name: "TemplateKey",
                table: "WritingExercises");

            migrationBuilder.DropColumn(
                name: "TemplateVersion",
                table: "WritingExercises");
        }
    }
}
