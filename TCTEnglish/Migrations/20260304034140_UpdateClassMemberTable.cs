using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCTVocabulary.Migrations
{
    /// <inheritdoc />
    public partial class UpdateClassMemberTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClassMembers_Classes_ClassID",
                table: "ClassMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_ClassMembers_Users_UserID",
                table: "ClassMembers");

            migrationBuilder.AddColumn<int>(
                name: "ClassId",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: 1,
                column: "ClassId",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_Users_ClassId",
                table: "Users",
                column: "ClassId");

            migrationBuilder.AddForeignKey(
                name: "FK_ClassMembers_Classes_ClassID",
                table: "ClassMembers",
                column: "ClassID",
                principalTable: "Classes",
                principalColumn: "ClassID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ClassMembers_Users_UserID",
                table: "ClassMembers",
                column: "UserID",
                principalTable: "Users",
                principalColumn: "UserID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Classes_ClassId",
                table: "Users",
                column: "ClassId",
                principalTable: "Classes",
                principalColumn: "ClassID");
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

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Classes_ClassId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_ClassId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ClassId",
                table: "Users");

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
    }
}
