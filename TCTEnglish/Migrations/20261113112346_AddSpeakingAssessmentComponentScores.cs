using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCTEnglish.Migrations
{
    /// <inheritdoc />
    public partial class AddSpeakingAssessmentComponentScores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "CompletenessScore",
                table: "SpeakingAssessments",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "FluencyScore",
                table: "SpeakingAssessments",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MetricVersion",
                table: "SpeakingAssessments",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletenessScore",
                table: "SpeakingAssessments");

            migrationBuilder.DropColumn(
                name: "FluencyScore",
                table: "SpeakingAssessments");

            migrationBuilder.DropColumn(
                name: "MetricVersion",
                table: "SpeakingAssessments");
        }
    }
}
