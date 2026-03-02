using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCTVocabulary.Migrations
{
    /// <inheritdoc />
    public partial class AddSpeakingPlaylist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ThumbnailUrl",
                table: "SpeakingVideos",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AddColumn<string>(
                name: "Duration",
                table: "SpeakingVideos",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlaylistId",
                table: "SpeakingVideos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "SpeakingPlaylists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpeakingPlaylists", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SpeakingVideos_PlaylistId",
                table: "SpeakingVideos",
                column: "PlaylistId");

            migrationBuilder.AddForeignKey(
                name: "FK_SpeakingVideos_SpeakingPlaylists",
                table: "SpeakingVideos",
                column: "PlaylistId",
                principalTable: "SpeakingPlaylists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SpeakingVideos_SpeakingPlaylists",
                table: "SpeakingVideos");

            migrationBuilder.DropTable(
                name: "SpeakingPlaylists");

            migrationBuilder.DropIndex(
                name: "IX_SpeakingVideos_PlaylistId",
                table: "SpeakingVideos");

            migrationBuilder.DropColumn(
                name: "Duration",
                table: "SpeakingVideos");

            migrationBuilder.DropColumn(
                name: "PlaylistId",
                table: "SpeakingVideos");

            migrationBuilder.AlterColumn<string>(
                name: "ThumbnailUrl",
                table: "SpeakingVideos",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);
        }
    }
}
