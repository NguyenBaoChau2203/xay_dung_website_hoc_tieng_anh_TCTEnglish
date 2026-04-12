using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCTVocabulary.Migrations
{
    /// <inheritdoc />
    public partial class AddListeningFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Robust cleanup: Drop all foreign keys referencing ListeningLessons, then drop the tables
            migrationBuilder.Sql(@"
                DECLARE @sql NVARCHAR(MAX) = '';
                SELECT @sql += 'ALTER TABLE ' + QUOTENAME(SCHEMA_NAME(t.schema_id)) + '.' + QUOTENAME(t.name) + 
                               ' DROP CONSTRAINT ' + QUOTENAME(fk.name) + ';'
                FROM sys.foreign_keys AS fk
                INNER JOIN sys.tables AS t ON fk.parent_object_id = t.object_id
                INNER JOIN sys.tables AS rt ON fk.referenced_object_id = rt.object_id
                WHERE rt.name = 'ListeningLessons';
                EXEC sp_executesql @sql;

                IF OBJECT_ID('dbo.UserListeningProgresses', 'U') IS NOT NULL DROP TABLE dbo.UserListeningProgresses;
                IF OBJECT_ID('dbo.ListeningQuizQuestions', 'U') IS NOT NULL DROP TABLE dbo.ListeningQuizQuestions;
                IF OBJECT_ID('dbo.ListeningTranscriptLines', 'U') IS NOT NULL DROP TABLE dbo.ListeningTranscriptLines;
                IF OBJECT_ID('dbo.ListeningVocabItems', 'U') IS NOT NULL DROP TABLE dbo.ListeningVocabItems;
                IF OBJECT_ID('dbo.ListeningLessons', 'U') IS NOT NULL DROP TABLE dbo.ListeningLessons;
            ");

            migrationBuilder.CreateTable(
                name: "ListeningLessons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Level = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Topic = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    YoutubeId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AudioUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Duration = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Speaker1Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Speaker2Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Speaker1Country = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Speaker2Country = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListeningLessons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ListeningQuizQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LessonId = table.Column<int>(type: "int", nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false),
                    QuestionText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OptionA = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OptionB = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OptionC = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OptionD = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CorrectAnswer = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    Explanation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListeningQuizQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ListeningQuizQuestions_ListeningLessons",
                        column: x => x.LessonId,
                        principalTable: "ListeningLessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ListeningTranscriptLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LessonId = table.Column<int>(type: "int", nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false),
                    Speaker = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VietnameseMeaning = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartTime = table.Column<double>(type: "float", nullable: true),
                    EndTime = table.Column<double>(type: "float", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListeningTranscriptLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ListeningTranscriptLines_ListeningLessons",
                        column: x => x.LessonId,
                        principalTable: "ListeningLessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ListeningVocabItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LessonId = table.Column<int>(type: "int", nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false),
                    Word = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Definition = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExampleSentence = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListeningVocabItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ListeningVocabItems_ListeningLessons",
                        column: x => x.LessonId,
                        principalTable: "ListeningLessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserListeningProgresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    LessonId = table.Column<int>(type: "int", nullable: false),
                    TranscriptCompleted = table.Column<bool>(type: "bit", nullable: false),
                    QuizCompleted = table.Column<bool>(type: "bit", nullable: false),
                    QuizScore = table.Column<int>(type: "int", nullable: true),
                    VocabReviewed = table.Column<bool>(type: "bit", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastAccessedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserListeningProgresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserListeningProgress_ListeningLessons",
                        column: x => x.LessonId,
                        principalTable: "ListeningLessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserListeningProgress_Users",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ListeningLessons_Published_Level_Topic",
                table: "ListeningLessons",
                columns: new[] { "IsPublished", "Level", "Topic" });

            migrationBuilder.CreateIndex(
                name: "IX_ListeningQuizQuestions_LessonId_OrderIndex",
                table: "ListeningQuizQuestions",
                columns: new[] { "LessonId", "OrderIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ListeningTranscriptLines_LessonId_OrderIndex",
                table: "ListeningTranscriptLines",
                columns: new[] { "LessonId", "OrderIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ListeningVocabItems_LessonId_OrderIndex",
                table: "ListeningVocabItems",
                columns: new[] { "LessonId", "OrderIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserListeningProgress_UserId_LessonId",
                table: "UserListeningProgresses",
                columns: new[] { "UserId", "LessonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserListeningProgresses_LessonId",
                table: "UserListeningProgresses",
                column: "LessonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Empty during sync operation to prevent errors when tables don't exist yet
        }
    }
}
