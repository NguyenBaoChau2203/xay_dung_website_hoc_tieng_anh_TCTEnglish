using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCTVocabulary.Migrations
{
    /// <inheritdoc />
    public partial class AddSpeakingOwnershipImportMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SpeakingSentences_VideoId",
                table: "SpeakingSentences");

            migrationBuilder.AlterColumn<string>(
                name: "Topic",
                table: "SpeakingVideos",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<int>(
                name: "PlaylistId",
                table: "SpeakingVideos",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "Level",
                table: "SpeakingVideos",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "SpeakingVideos",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<string>(
                name: "ImportStatus",
                table: "SpeakingVideos",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "ready");

            migrationBuilder.AddColumn<int>(
                name: "OwnerUserId",
                table: "SpeakingVideos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "SpeakingVideos",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "admin");

            migrationBuilder.AddColumn<string>(
                name: "SourceUrl",
                table: "SpeakingVideos",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TranscriptSource",
                table: "SpeakingVideos",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "VietnameseMeaning",
                table: "SpeakingSentences",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_SpeakingVideos_OwnerUserId_CreatedAt",
                table: "SpeakingVideos",
                columns: new[] { "OwnerUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SpeakingVideos_OwnerUserId_YoutubeId",
                table: "SpeakingVideos",
                columns: new[] { "OwnerUserId", "YoutubeId" },
                unique: true,
                filter: "[OwnerUserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SpeakingSentences_VideoId_StartTime",
                table: "SpeakingSentences",
                columns: new[] { "VideoId", "StartTime" });

            migrationBuilder.AddForeignKey(
                name: "FK_SpeakingVideos_Users_OwnerUserId",
                table: "SpeakingVideos",
                column: "OwnerUserId",
                principalTable: "Users",
                principalColumn: "UserID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SpeakingVideos_Users_OwnerUserId",
                table: "SpeakingVideos");

            migrationBuilder.DropIndex(
                name: "IX_SpeakingVideos_OwnerUserId_CreatedAt",
                table: "SpeakingVideos");

            migrationBuilder.DropIndex(
                name: "IX_SpeakingVideos_OwnerUserId_YoutubeId",
                table: "SpeakingVideos");

            migrationBuilder.DropIndex(
                name: "IX_SpeakingSentences_VideoId_StartTime",
                table: "SpeakingSentences");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "SpeakingVideos");

            migrationBuilder.DropColumn(
                name: "ImportStatus",
                table: "SpeakingVideos");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "SpeakingVideos");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "SpeakingVideos");

            migrationBuilder.DropColumn(
                name: "SourceUrl",
                table: "SpeakingVideos");

            migrationBuilder.DropColumn(
                name: "TranscriptSource",
                table: "SpeakingVideos");

            migrationBuilder.AlterColumn<string>(
                name: "Topic",
                table: "SpeakingVideos",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "PlaylistId",
                table: "SpeakingVideos",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Level",
                table: "SpeakingVideos",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "VietnameseMeaning",
                table: "SpeakingSentences",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldDefaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_SpeakingSentences_VideoId",
                table: "SpeakingSentences",
                column: "VideoId");
        }
    }
}
