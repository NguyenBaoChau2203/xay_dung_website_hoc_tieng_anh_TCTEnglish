using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TCTVocabulary.Models;

#nullable disable

namespace TCTEnglish.Migrations
{
    /// <summary>
    /// Additive provenance hash for speaking import jobs.
    /// Existing rows stay null and keep the legacy caption meaning of SourceKind null.
    /// </summary>
    [DbContext(typeof(DbflashcardContext))]
    [Migration("20261030113000_LearningImportSourceHash")]
    public partial class LearningImportSourceHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceContentHash",
                table: "SpeakingImportJobs",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceContentHash",
                table: "SpeakingImportJobs");
        }
    }
}
