using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCTVocabulary.Migrations
{
    /// <inheritdoc />
    public partial class AddLockFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LockExpiry",
                table: "Users",
                type: "datetime",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LockReason",
                table: "Users",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LockExpiry",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LockReason",
                table: "Users");
        }
    }
}
