using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCTVocabulary.Migrations
{
    /// <inheritdoc />
    public partial class AddUserDailyActivityAreaCountersPhase2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ListeningCompletedCount",
                table: "UserDailyActivities",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ReadingCompletedCount",
                table: "UserDailyActivities",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VocabularyCompletedCount",
                table: "UserDailyActivities",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WritingCompletedCount",
                table: "UserDailyActivities",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ListeningCompletedCount",
                table: "UserDailyActivities");

            migrationBuilder.DropColumn(
                name: "ReadingCompletedCount",
                table: "UserDailyActivities");

            migrationBuilder.DropColumn(
                name: "VocabularyCompletedCount",
                table: "UserDailyActivities");

            migrationBuilder.DropColumn(
                name: "WritingCompletedCount",
                table: "UserDailyActivities");
        }
    }
}
