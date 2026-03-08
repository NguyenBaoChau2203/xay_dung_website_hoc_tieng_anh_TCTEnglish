using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCTVocabulary.Migrations
{
    /// <inheritdoc />
    public partial class ChangeUserStatusToEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add the new Status column (int) with default = 0 (Offline)
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // 2. Migrate data: IsActive = true → Online (1), false → Offline (0)
            migrationBuilder.Sql(
                "UPDATE [Users] SET [Status] = CASE WHEN [IsActive] = 1 THEN 1 ELSE 0 END");

            // 3. Drop the old boolean column
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 1. Re-add the old boolean column
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);

            // 2. Migrate data back: Online (1) → true, anything else → false
            migrationBuilder.Sql(
                "UPDATE [Users] SET [IsActive] = CASE WHEN [Status] = 1 THEN 1 ELSE 0 END");

            // 3. Drop the Status column
            migrationBuilder.DropColumn(
                name: "Status",
                table: "Users");
        }
    }
}
