using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCTVocabulary.Migrations
{
    /// <inheritdoc />
    public partial class AddClassFolderTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClassFolders",
                columns: table => new
                {
                    ClassFolderID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClassID = table.Column<int>(type: "int", nullable: false),
                    FolderID = table.Column<int>(type: "int", nullable: false),
                    AddedByUserID = table.Column<int>(type: "int", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassFolders", x => x.ClassFolderID);
                    table.ForeignKey(
                        name: "FK_ClassFolders_Classes_ClassID",
                        column: x => x.ClassID,
                        principalTable: "Classes",
                        principalColumn: "ClassID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassFolders_Folders_FolderID",
                        column: x => x.FolderID,
                        principalTable: "Folders",
                        principalColumn: "FolderID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassFolders_Users_AddedByUserID",
                        column: x => x.AddedByUserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClassFolders_AddedByUserID",
                table: "ClassFolders",
                column: "AddedByUserID");

            migrationBuilder.CreateIndex(
                name: "IX_ClassFolders_ClassID_FolderID",
                table: "ClassFolders",
                columns: new[] { "ClassID", "FolderID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClassFolders_FolderID",
                table: "ClassFolders",
                column: "FolderID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClassFolders");
        }
    }
}
