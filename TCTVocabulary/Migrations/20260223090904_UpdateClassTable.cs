using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCTVocabulary.Migrations
{
    /// <inheritdoc />
    public partial class UpdateClassTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK__ClassMemb__Class__440B1D61",
                table: "ClassMembers");

            migrationBuilder.DropForeignKey(
                name: "FK__ClassMemb__UserI__44FF419A",
                table: "ClassMembers");

            migrationBuilder.DropPrimaryKey(
                name: "PK__ClassMem__1A61AB6A6D3064A5",
                table: "ClassMembers");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Classes",
                type: "datetime",
                nullable: false,
                defaultValueSql: "(getdate())");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Classes",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasPassword",
                table: "Classes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Classes",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "Classes",
                type: "varchar(255)",
                unicode: false,
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ClassMembers",
                table: "ClassMembers",
                columns: new[] { "ClassID", "UserID" });

            migrationBuilder.AddForeignKey(
                name: "FK_ClassMembers_Classes_ClassID",
                table: "ClassMembers",
                column: "ClassID",
                principalTable: "Classes",
                principalColumn: "ClassID");

            migrationBuilder.AddForeignKey(
                name: "FK_ClassMembers_Users_UserID",
                table: "ClassMembers",
                column: "UserID",
                principalTable: "Users",
                principalColumn: "UserID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClassMembers_Classes_ClassID",
                table: "ClassMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_ClassMembers_Users_UserID",
                table: "ClassMembers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ClassMembers",
                table: "ClassMembers");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Classes");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Classes");

            migrationBuilder.DropColumn(
                name: "HasPassword",
                table: "Classes");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Classes");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "Classes");

            migrationBuilder.AddPrimaryKey(
                name: "PK__ClassMem__1A61AB6A6D3064A5",
                table: "ClassMembers",
                columns: new[] { "ClassID", "UserID" });

            migrationBuilder.AddForeignKey(
                name: "FK__ClassMemb__Class__440B1D61",
                table: "ClassMembers",
                column: "ClassID",
                principalTable: "Classes",
                principalColumn: "ClassID");

            migrationBuilder.AddForeignKey(
                name: "FK__ClassMemb__UserI__44FF419A",
                table: "ClassMembers",
                column: "UserID",
                principalTable: "Users",
                principalColumn: "UserID");
        }
    }
}
