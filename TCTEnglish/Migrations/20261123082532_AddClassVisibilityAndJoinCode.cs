using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCTEnglish.Migrations
{
    /// <inheritdoc />
    public partial class AddClassVisibilityAndJoinCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "Classes",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "JoinCode",
                table: "Classes",
                type: "varchar(16)",
                maxLength: 16,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            // Unique, stable backfill for existing rows (CL + zero-padded ClassID).
            migrationBuilder.Sql("""
                UPDATE `Classes`
                SET `JoinCode` = CONCAT('CL', LPAD(CAST(`ClassID` AS CHAR), 6, '0'))
                WHERE `JoinCode` IS NULL OR `JoinCode` = '';
                """);

            migrationBuilder.AlterColumn<string>(
                name: "JoinCode",
                table: "Classes",
                type: "varchar(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(16)",
                oldMaxLength: 16,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Classes_JoinCode",
                table: "Classes",
                column: "JoinCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Classes_JoinCode",
                table: "Classes");

            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "Classes");

            migrationBuilder.DropColumn(
                name: "JoinCode",
                table: "Classes");
        }
    }
}
