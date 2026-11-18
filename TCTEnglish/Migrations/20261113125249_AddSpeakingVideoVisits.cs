using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCTEnglish.Migrations
{
    /// <inheritdoc />
    public partial class AddSpeakingVideoVisits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserSpeakingVideoVisits",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    VideoId = table.Column<int>(type: "int", nullable: false),
                    LastOpenedAtUtc = table.Column<DateTime>(type: "datetime(6)", precision: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSpeakingVideoVisits", x => new { x.UserId, x.VideoId });
                    table.ForeignKey(
                        name: "FK_UserSpeakingVideoVisits_SpeakingVideos",
                        column: x => x.VideoId,
                        principalTable: "SpeakingVideos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserSpeakingVideoVisits_Users",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_UserSpeakingVideoVisits_UserId_LastOpenedAtUtc_VideoId",
                table: "UserSpeakingVideoVisits",
                columns: new[] { "UserId", "LastOpenedAtUtc", "VideoId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserSpeakingVideoVisits_VideoId",
                table: "UserSpeakingVideoVisits",
                column: "VideoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserSpeakingVideoVisits");
        }
    }
}
