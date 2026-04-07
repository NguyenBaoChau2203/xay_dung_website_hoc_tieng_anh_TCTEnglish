using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCTEnglish.Migrations
{
    /// <inheritdoc />
    public partial class AddWritingAttemptFeedbackSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GrammarFeedback",
                table: "UserWritingAttempts",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MeaningFeedback",
                table: "UserWritingAttempts",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NaturalnessFeedback",
                table: "UserWritingAttempts",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewText",
                table: "UserWritingAttempts",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuggestedRewrite",
                table: "UserWritingAttempts",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SummaryText",
                table: "UserWritingAttempts",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SummaryTitle",
                table: "UserWritingAttempts",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WordChoiceFeedback",
                table: "UserWritingAttempts",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GrammarFeedback",
                table: "UserWritingAttempts");

            migrationBuilder.DropColumn(
                name: "MeaningFeedback",
                table: "UserWritingAttempts");

            migrationBuilder.DropColumn(
                name: "NaturalnessFeedback",
                table: "UserWritingAttempts");

            migrationBuilder.DropColumn(
                name: "ReviewText",
                table: "UserWritingAttempts");

            migrationBuilder.DropColumn(
                name: "SuggestedRewrite",
                table: "UserWritingAttempts");

            migrationBuilder.DropColumn(
                name: "SummaryText",
                table: "UserWritingAttempts");

            migrationBuilder.DropColumn(
                name: "SummaryTitle",
                table: "UserWritingAttempts");

            migrationBuilder.DropColumn(
                name: "WordChoiceFeedback",
                table: "UserWritingAttempts");
        }
    }
}
