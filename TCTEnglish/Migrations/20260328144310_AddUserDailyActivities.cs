using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCTVocabulary.Migrations
{
    /// <inheritdoc />
    public partial class AddUserDailyActivities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserDailyActivities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    ActivityDate = table.Column<DateTime>(type: "date", nullable: false),
                    XpEarned = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CardsReviewed = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    NewCardsLearned = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    QuizzesCompleted = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    SpeakingCompletedCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDailyActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserDailyActivities_Users",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserDailyActivities_UserId_ActivityDate",
                table: "UserDailyActivities",
                columns: new[] { "UserID", "ActivityDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserDailyActivities");
        }
    }
}
