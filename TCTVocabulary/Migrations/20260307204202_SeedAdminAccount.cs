using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCTVocabulary.Migrations
{
    /// <inheritdoc />
    public partial class SeedAdminAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK__Folders__ParentF__3E52440B",
                table: "Folders");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Classes_ClassId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_ClassId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ClassId",
                table: "Users");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AlterColumn<string>(
                name: "Topic",
                table: "SpeakingVideos",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "AddedAt",
                table: "ClassFolders",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "getutcdate()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "GETDATE()");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: 1,
                columns: new[] { "Email", "FullName", "IsActive", "PasswordHash", "Role" },
                values: new object[] { "admin@tctenglish.com", "System Admin", true, "$2a$11$P/Ddyz.mGnpom9fEbTcXxuaOmUYMAaCZDKac8vCTJOY6GK4LzYR2y", "Admin" });

            migrationBuilder.AddForeignKey(
                name: "FK__Folders__ParentF__3E52440B",
                table: "Folders",
                column: "ParentFolderID",
                principalTable: "Folders",
                principalColumn: "FolderID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK__Folders__ParentF__3E52440B",
                table: "Folders");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Users");

            migrationBuilder.AddColumn<int>(
                name: "ClassId",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Topic",
                table: "SpeakingVideos",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<DateTime>(
                name: "AddedAt",
                table: "ClassFolders",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETDATE()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "getutcdate()");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: 1,
                columns: new[] { "ClassId", "Email", "FullName", "PasswordHash", "Role" },
                values: new object[] { null, "baochau1512v@gmail.com", "chau2203", "$2a$11$.ryXFi1l5E2Bomp0jygOMuPyhcqbpwpMuOHB79oBcAe9idsouoL6u", "User" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_ClassId",
                table: "Users",
                column: "ClassId");

            migrationBuilder.AddForeignKey(
                name: "FK__Folders__ParentF__3E52440B",
                table: "Folders",
                column: "ParentFolderID",
                principalTable: "Folders",
                principalColumn: "FolderID");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Classes_ClassId",
                table: "Users",
                column: "ClassId",
                principalTable: "Classes",
                principalColumn: "ClassID");
        }
    }
}
