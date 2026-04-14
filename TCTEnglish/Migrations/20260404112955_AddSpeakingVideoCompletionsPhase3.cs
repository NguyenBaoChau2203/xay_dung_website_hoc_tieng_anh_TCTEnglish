using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TCTVocabulary.Migrations
{
    /// <inheritdoc />
    public partial class AddSpeakingVideoCompletionsPhase3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserSpeakingVideoCompletions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    VideoID = table.Column<int>(type: "int", nullable: false),
                    CompletedSentenceCount = table.Column<int>(type: "int", nullable: false),
                    RequiredSentenceCount = table.Column<int>(type: "int", nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastEvaluatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSpeakingVideoCompletions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSpeakingVideoCompletions_SpeakingVideos",
                        column: x => x.VideoID,
                        principalTable: "SpeakingVideos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserSpeakingVideoCompletions_Users",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Badges",
                columns: new[] { "Id", "Code", "Description", "IconClass", "MetricType", "Name", "SortOrder", "ThresholdValue" },
                values: new object[,]
                {
                    { 7, "speaking-first-video", "Hoàn thành video Speaking đầu tiên.", "fas fa-microphone", 5, "Khởi động Speaking", 7, 1 },
                    { 8, "speaking-five-videos", "Hoàn thành 5 video Speaking để duy trì luyện tập.", "fas fa-comments", 5, "Nói trôi chảy", 8, 5 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserSpeakingVideoCompletions_UserId_VideoId",
                table: "UserSpeakingVideoCompletions",
                columns: new[] { "UserID", "VideoID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSpeakingVideoCompletions_VideoID",
                table: "UserSpeakingVideoCompletions",
                column: "VideoID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserSpeakingVideoCompletions");

            migrationBuilder.DeleteData(
                table: "Badges",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Badges",
                keyColumn: "Id",
                keyValue: 8);
        }
    }
}
