using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCTVocabulary.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Email = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: false),
                    PasswordHash = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResetPasswordToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResetPasswordTokenExpiry = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Streak = table.Column<int>(type: "int", nullable: true, defaultValue: 0),
                    Goal = table.Column<int>(type: "int", nullable: true, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Users__1788CCAC46CDCFCA", x => x.UserID);
                });

            migrationBuilder.CreateTable(
                name: "Classes",
                columns: table => new
                {
                    ClassID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClassName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    OwnerID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Classes__CB1927A0B52422EC", x => x.ClassID);
                    table.ForeignKey(
                        name: "FK__Classes__OwnerID__412EB0B6",
                        column: x => x.OwnerID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "Folders",
                columns: table => new
                {
                    FolderID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    FolderName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ParentFolderID = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Folders__ACD7109F2ECFF2AF", x => x.FolderID);
                    table.ForeignKey(
                        name: "FK__Folders__ParentF__3E52440B",
                        column: x => x.ParentFolderID,
                        principalTable: "Folders",
                        principalColumn: "FolderID");
                    table.ForeignKey(
                        name: "FK__Folders__UserID__3D5E1FD2",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "ClassMembers",
                columns: table => new
                {
                    ClassID = table.Column<int>(type: "int", nullable: false),
                    UserID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__ClassMem__1A61AB6A6D3064A5", x => new { x.ClassID, x.UserID });
                    table.ForeignKey(
                        name: "FK__ClassMemb__Class__440B1D61",
                        column: x => x.ClassID,
                        principalTable: "Classes",
                        principalColumn: "ClassID");
                    table.ForeignKey(
                        name: "FK__ClassMemb__UserI__44FF419A",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "Sets",
                columns: table => new
                {
                    SetID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SetName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    OwnerID = table.Column<int>(type: "int", nullable: false),
                    FolderID = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Sets__7E08473D47BDA11E", x => x.SetID);
                    table.ForeignKey(
                        name: "FK__Sets__FolderID__49C3F6B7",
                        column: x => x.FolderID,
                        principalTable: "Folders",
                        principalColumn: "FolderID");
                    table.ForeignKey(
                        name: "FK__Sets__OwnerID__48CFD27E",
                        column: x => x.OwnerID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "Cards",
                columns: table => new
                {
                    CardID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SetID = table.Column<int>(type: "int", nullable: false),
                    Term = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Definition = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Cards__55FECD8E2103FA66", x => x.CardID);
                    table.ForeignKey(
                        name: "FK__Cards__SetID__4CA06362",
                        column: x => x.SetID,
                        principalTable: "Sets",
                        principalColumn: "SetID");
                });

            migrationBuilder.CreateTable(
                name: "LearningProgress",
                columns: table => new
                {
                    ProgressID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    CardID = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    WrongCount = table.Column<int>(type: "int", nullable: true, defaultValue: 0),
                    LastReviewedDate = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Learning__BAE29C8531677C23", x => x.ProgressID);
                    table.ForeignKey(
                        name: "FK__LearningP__CardI__5441852A",
                        column: x => x.CardID,
                        principalTable: "Cards",
                        principalColumn: "CardID");
                    table.ForeignKey(
                        name: "FK__LearningP__UserI__534D60F1",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cards_SetID",
                table: "Cards",
                column: "SetID");

            migrationBuilder.CreateIndex(
                name: "IX_Classes_OwnerID",
                table: "Classes",
                column: "OwnerID");

            migrationBuilder.CreateIndex(
                name: "IX_ClassMembers_UserID",
                table: "ClassMembers",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_Folders_ParentFolderID",
                table: "Folders",
                column: "ParentFolderID");

            migrationBuilder.CreateIndex(
                name: "IX_Folders_UserID",
                table: "Folders",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_LearningProgress_CardID",
                table: "LearningProgress",
                column: "CardID");

            migrationBuilder.CreateIndex(
                name: "UQ__Learning__E2D72075740E222A",
                table: "LearningProgress",
                columns: new[] { "UserID", "CardID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sets_FolderID",
                table: "Sets",
                column: "FolderID");

            migrationBuilder.CreateIndex(
                name: "IX_Sets_OwnerID",
                table: "Sets",
                column: "OwnerID");

            migrationBuilder.CreateIndex(
                name: "UQ__Users__A9D10534362A84D8",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClassMembers");

            migrationBuilder.DropTable(
                name: "LearningProgress");

            migrationBuilder.DropTable(
                name: "Classes");

            migrationBuilder.DropTable(
                name: "Cards");

            migrationBuilder.DropTable(
                name: "Sets");

            migrationBuilder.DropTable(
                name: "Folders");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
