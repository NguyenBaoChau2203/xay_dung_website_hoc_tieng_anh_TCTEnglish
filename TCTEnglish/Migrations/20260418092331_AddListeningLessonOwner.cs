using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCTVocabulary.Migrations
{
    /// <inheritdoc />
    public partial class AddListeningLessonOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OwnerUserId",
                table: "ListeningLessons",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TranscriptSource",
                table: "ListeningLessons",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ListeningLessons_OwnerUserId_CreatedAt",
                table: "ListeningLessons",
                columns: new[] { "OwnerUserId", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_ListeningLessons_Users_OwnerUserId",
                table: "ListeningLessons",
                column: "OwnerUserId",
                principalTable: "Users",
                principalColumn: "UserID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ListeningLessons_Users_OwnerUserId",
                table: "ListeningLessons");

            migrationBuilder.DropIndex(
                name: "IX_ListeningLessons_OwnerUserId_CreatedAt",
                table: "ListeningLessons");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "ListeningLessons");

            migrationBuilder.DropColumn(
                name: "TranscriptSource",
                table: "ListeningLessons");
        }
    }
}
