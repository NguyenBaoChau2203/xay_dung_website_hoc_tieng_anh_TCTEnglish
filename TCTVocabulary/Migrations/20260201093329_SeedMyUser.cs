using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCTVocabulary.Migrations
{
    /// <inheritdoc />
    public partial class SeedMyUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "UserID", "CreatedAt", "Email", "FullName", "Goal", "PasswordHash", "ResetPasswordToken", "ResetPasswordTokenExpiry", "Role", "Streak" },
                values: new object[] { 1, new DateTime(2026, 2, 1, 16, 33, 28, 913, DateTimeKind.Local).AddTicks(3678), "baochau1512v@gmail.com", "chau2203", 0, "$2a$11$.ryXFi1l5E2Bomp0jygOMuPyhcqbpwpMuOHB79oBcAe9idsouoL6u", null, null, "User", 0 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: 1);
        }
    }
}
