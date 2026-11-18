using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCTEnglish.Migrations
{
    /// <inheritdoc />
    public partial class AddReadingModuleSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReadingLevel",
                table: "Users",
                type: "varchar(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "A1")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CorrectAnswerText",
                table: "ReadingQuestions",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "EvidenceParagraphId",
                table: "ReadingQuestions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidenceText",
                table: "ReadingQuestions",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Explanation",
                table: "ReadingQuestions",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ExternalQuestionKey",
                table: "ReadingQuestions",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ParagraphReference",
                table: "ReadingQuestions",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Paraphrase",
                table: "ReadingQuestions",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "Points",
                table: "ReadingQuestions",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "QuestionType",
                table: "ReadingQuestions",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "MultipleChoice")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Topic",
                table: "ReadingPassages",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "ReadingPassages",
                type: "varchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Level",
                table: "ReadingPassages",
                type: "varchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "CreatedByUserId",
                table: "ReadingPassages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "ReadingPassages",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "EstimatedTimeMinutes",
                table: "ReadingPassages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalReadingKey",
                table: "ReadingPassages",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "ReadingPassages",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PublishedByUserId",
                table: "ReadingPassages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ReadingPassages",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Draft")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ReadingPassages",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE ReadingPassages
                SET Status = CASE WHEN IsPublished = 1 THEN 'Published' ELSE 'Draft' END,
                    UpdatedAt = COALESCE(UpdatedAt, CreatedAt)
                """);

            migrationBuilder.AddColumn<string>(
                name: "ExternalOptionKey",
                table: "ReadingOptions",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "MatchKey",
                table: "ReadingOptions",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "MatchSide",
                table: "ReadingOptions",
                type: "varchar(16)",
                maxLength: 16,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "OptionOrder",
                table: "ReadingOptions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ReadingAnnotations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ReadingPassageId = table.Column<int>(type: "int", nullable: false),
                    AnnotationType = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StartOffset = table.Column<int>(type: "int", nullable: false),
                    EndOffset = table.Column<int>(type: "int", nullable: false),
                    SelectedText = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NoteText = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Color = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadingAnnotations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReadingAnnotations_ReadingPassages_ReadingPassageId",
                        column: x => x.ReadingPassageId,
                        principalTable: "ReadingPassages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReadingAnnotations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ReadingAttempts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ReadingPassageId = table.Column<int>(type: "int", nullable: false),
                    Mode = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StartedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IsSubmitted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    TimeLimitSeconds = table.Column<int>(type: "int", nullable: true),
                    ElapsedSeconds = table.Column<int>(type: "int", nullable: true),
                    Score = table.Column<int>(type: "int", nullable: true),
                    MaxScore = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadingAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReadingAttempts_ReadingPassages_ReadingPassageId",
                        column: x => x.ReadingPassageId,
                        principalTable: "ReadingPassages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReadingAttempts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ReadingParagraphs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ReadingPassageId = table.Column<int>(type: "int", nullable: false),
                    ExternalParagraphKey = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ParagraphOrder = table.Column<int>(type: "int", nullable: false),
                    ParagraphContent = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadingParagraphs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReadingParagraphs_ReadingPassages_ReadingPassageId",
                        column: x => x.ReadingPassageId,
                        principalTable: "ReadingPassages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ReadingReviewLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ReadingPassageId = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<int>(type: "int", nullable: false),
                    FromStatus = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ToStatus = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Reason = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadingReviewLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReadingReviewLogs_ReadingPassages_ReadingPassageId",
                        column: x => x.ReadingPassageId,
                        principalTable: "ReadingPassages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReadingReviewLogs_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ReadingStudentAnswers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    AttemptId = table.Column<int>(type: "int", nullable: false),
                    QuestionId = table.Column<int>(type: "int", nullable: false),
                    SelectedOptionId = table.Column<int>(type: "int", nullable: true),
                    AnswerText = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MatchingJson = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsCorrect = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    IsFlagged = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadingStudentAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReadingStudentAnswers_ReadingAttempts_AttemptId",
                        column: x => x.AttemptId,
                        principalTable: "ReadingAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReadingStudentAnswers_ReadingOptions_SelectedOptionId",
                        column: x => x.SelectedOptionId,
                        principalTable: "ReadingOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ReadingStudentAnswers_ReadingQuestions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "ReadingQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ReadingVocabularyItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ReadingPassageId = table.Column<int>(type: "int", nullable: false),
                    ExternalVocabularyKey = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Word = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Ipa = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PartOfSpeech = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EnglishDefinition = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    VietnameseMeaning = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Example = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ParagraphId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadingVocabularyItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReadingVocabularyItems_ReadingParagraphs_ParagraphId",
                        column: x => x.ParagraphId,
                        principalTable: "ReadingParagraphs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ReadingVocabularyItems_ReadingPassages_ReadingPassageId",
                        column: x => x.ReadingPassageId,
                        principalTable: "ReadingPassages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "StudentVocabularies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    SourceReadingPassageId = table.Column<int>(type: "int", nullable: true),
                    SourceVocabularyItemId = table.Column<int>(type: "int", nullable: true),
                    Word = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Ipa = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PartOfSpeech = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EnglishDefinition = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    VietnameseMeaning = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Example = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentVocabularies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentVocabularies_ReadingPassages_SourceReadingPassageId",
                        column: x => x.SourceReadingPassageId,
                        principalTable: "ReadingPassages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StudentVocabularies_ReadingVocabularyItems_SourceVocabularyI~",
                        column: x => x.SourceVocabularyItemId,
                        principalTable: "ReadingVocabularyItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StudentVocabularies_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingQuestions_EvidenceParagraphId",
                table: "ReadingQuestions",
                column: "EvidenceParagraphId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingPassages_CreatedByUserId",
                table: "ReadingPassages",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingPassages_IsPublished_Level",
                table: "ReadingPassages",
                columns: new[] { "IsPublished", "Level" });

            migrationBuilder.CreateIndex(
                name: "IX_ReadingPassages_PublishedByUserId",
                table: "ReadingPassages",
                column: "PublishedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingPassages_Status",
                table: "ReadingPassages",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingAnnotations_ReadingPassageId",
                table: "ReadingAnnotations",
                column: "ReadingPassageId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingAnnotations_UserId",
                table: "ReadingAnnotations",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingAttempts_ReadingPassageId",
                table: "ReadingAttempts",
                column: "ReadingPassageId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingAttempts_User_Passage_Started",
                table: "ReadingAttempts",
                columns: new[] { "UserId", "ReadingPassageId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReadingParagraphs_Passage_Order",
                table: "ReadingParagraphs",
                columns: new[] { "ReadingPassageId", "ParagraphOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ReadingReviewLogs_ActorUserId",
                table: "ReadingReviewLogs",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingReviewLogs_Passage",
                table: "ReadingReviewLogs",
                column: "ReadingPassageId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingStudentAnswers_Attempt_Question",
                table: "ReadingStudentAnswers",
                columns: new[] { "AttemptId", "QuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReadingStudentAnswers_QuestionId",
                table: "ReadingStudentAnswers",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingStudentAnswers_SelectedOptionId",
                table: "ReadingStudentAnswers",
                column: "SelectedOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingVocabularyItems_ParagraphId",
                table: "ReadingVocabularyItems",
                column: "ParagraphId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingVocabularyItems_ReadingPassageId",
                table: "ReadingVocabularyItems",
                column: "ReadingPassageId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentVocabularies_SourceReadingPassageId",
                table: "StudentVocabularies",
                column: "SourceReadingPassageId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentVocabularies_SourceVocabularyItemId",
                table: "StudentVocabularies",
                column: "SourceVocabularyItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentVocabularies_User_Word",
                table: "StudentVocabularies",
                columns: new[] { "UserId", "Word" });

            migrationBuilder.AddForeignKey(
                name: "FK_ReadingPassages_Users_CreatedByUserId",
                table: "ReadingPassages",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "UserID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ReadingPassages_Users_PublishedByUserId",
                table: "ReadingPassages",
                column: "PublishedByUserId",
                principalTable: "Users",
                principalColumn: "UserID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ReadingQuestions_ReadingParagraphs_EvidenceParagraphId",
                table: "ReadingQuestions",
                column: "EvidenceParagraphId",
                principalTable: "ReadingParagraphs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReadingPassages_Users_CreatedByUserId",
                table: "ReadingPassages");

            migrationBuilder.DropForeignKey(
                name: "FK_ReadingPassages_Users_PublishedByUserId",
                table: "ReadingPassages");

            migrationBuilder.DropForeignKey(
                name: "FK_ReadingQuestions_ReadingParagraphs_EvidenceParagraphId",
                table: "ReadingQuestions");

            migrationBuilder.DropTable(
                name: "ReadingAnnotations");

            migrationBuilder.DropTable(
                name: "ReadingReviewLogs");

            migrationBuilder.DropTable(
                name: "ReadingStudentAnswers");

            migrationBuilder.DropTable(
                name: "StudentVocabularies");

            migrationBuilder.DropTable(
                name: "ReadingAttempts");

            migrationBuilder.DropTable(
                name: "ReadingVocabularyItems");

            migrationBuilder.DropTable(
                name: "ReadingParagraphs");

            migrationBuilder.DropIndex(
                name: "IX_ReadingQuestions_EvidenceParagraphId",
                table: "ReadingQuestions");

            migrationBuilder.DropIndex(
                name: "IX_ReadingPassages_CreatedByUserId",
                table: "ReadingPassages");

            migrationBuilder.DropIndex(
                name: "IX_ReadingPassages_IsPublished_Level",
                table: "ReadingPassages");

            migrationBuilder.DropIndex(
                name: "IX_ReadingPassages_PublishedByUserId",
                table: "ReadingPassages");

            migrationBuilder.DropIndex(
                name: "IX_ReadingPassages_Status",
                table: "ReadingPassages");

            migrationBuilder.DropColumn(
                name: "ReadingLevel",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CorrectAnswerText",
                table: "ReadingQuestions");

            migrationBuilder.DropColumn(
                name: "EvidenceParagraphId",
                table: "ReadingQuestions");

            migrationBuilder.DropColumn(
                name: "EvidenceText",
                table: "ReadingQuestions");

            migrationBuilder.DropColumn(
                name: "Explanation",
                table: "ReadingQuestions");

            migrationBuilder.DropColumn(
                name: "ExternalQuestionKey",
                table: "ReadingQuestions");

            migrationBuilder.DropColumn(
                name: "ParagraphReference",
                table: "ReadingQuestions");

            migrationBuilder.DropColumn(
                name: "Paraphrase",
                table: "ReadingQuestions");

            migrationBuilder.DropColumn(
                name: "Points",
                table: "ReadingQuestions");

            migrationBuilder.DropColumn(
                name: "QuestionType",
                table: "ReadingQuestions");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "ReadingPassages");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "ReadingPassages");

            migrationBuilder.DropColumn(
                name: "EstimatedTimeMinutes",
                table: "ReadingPassages");

            migrationBuilder.DropColumn(
                name: "ExternalReadingKey",
                table: "ReadingPassages");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "ReadingPassages");

            migrationBuilder.DropColumn(
                name: "PublishedByUserId",
                table: "ReadingPassages");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ReadingPassages");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ReadingPassages");

            migrationBuilder.DropColumn(
                name: "ExternalOptionKey",
                table: "ReadingOptions");

            migrationBuilder.DropColumn(
                name: "MatchKey",
                table: "ReadingOptions");

            migrationBuilder.DropColumn(
                name: "MatchSide",
                table: "ReadingOptions");

            migrationBuilder.DropColumn(
                name: "OptionOrder",
                table: "ReadingOptions");

            migrationBuilder.AlterColumn<string>(
                name: "Topic",
                table: "ReadingPassages",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(200)",
                oldMaxLength: 200,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "ReadingPassages",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(500)",
                oldMaxLength: 500)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Level",
                table: "ReadingPassages",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(10)",
                oldMaxLength: 10)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
