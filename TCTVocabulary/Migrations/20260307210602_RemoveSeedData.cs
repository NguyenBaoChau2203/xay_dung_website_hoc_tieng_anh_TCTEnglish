using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCTVocabulary.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSeedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "UserID", "AvatarUrl", "CreatedAt", "Email", "FullName", "Goal", "IsActive", "LastStudyDate", "LongestStreak", "PasswordHash", "ResetPasswordToken", "ResetPasswordTokenExpiry", "Role", "Streak" },
                values: new object[] { 1, null, new DateTime(2026, 2, 1, 16, 20, 51, 117, DateTimeKind.Unspecified), "admin@tctenglish.com", "System Admin", 0, true, null, 0, "$2a$11$P/Ddyz.mGnpom9fEbTcXxuaOmUYMAaCZDKac8vCTJOY6GK4LzYR2y", null, null, "Admin", 0 });
        }
    }
}
