using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCTEnglish.Migrations
{
    /// <inheritdoc />
    public partial class GuardLegacyReadingLengths : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Intentionally ordered immediately before the already-authored Reading
            // schema migration. MariaDB DDL is not transactional: validate every
            // narrowing bound before that migration adds its first column.
            migrationBuilder.Sql("""
                BEGIN NOT ATOMIC
                    IF EXISTS (
                        SELECT 1 FROM ReadingPassages
                        WHERE CHAR_LENGTH(Title) > 500
                           OR CHAR_LENGTH(Topic) > 200
                           OR CHAR_LENGTH(Level) > 10
                    ) THEN
                        SIGNAL SQLSTATE '45000'
                            SET MESSAGE_TEXT = 'Reading upgrade blocked: legacy values exceed Title 500, Topic 200 or Level 10. Preserve and review the content before retrying.';
                    END IF;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Validation only. No application data/schema was changed by this guard.
        }
    }
}
