using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCTEnglish.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonalSetWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The composite index must exist (and can back the SetID foreign key) before the
            // single-column index is dropped, otherwise MariaDB rejects the drop (error 1553).
            migrationBuilder.CreateIndex(
                name: "IX_Cards_SetId_Term",
                table: "Cards",
                columns: new[] { "SetID", "Term" });

            migrationBuilder.DropIndex(
                name: "IX_Cards_SetID",
                table: "Cards");

            migrationBuilder.AddColumn<int>(
                name: "ClonedFromSetId",
                table: "Sets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShareSlug",
                table: "Sets",
                type: "varchar(32)",
                unicode: false,
                maxLength: 32,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Sets",
                type: "datetime",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Visibility",
                table: "Sets",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: false,
                defaultValue: "Public")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "IsSuspended",
                table: "LearningProgress",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AiGenerated",
                table: "Cards",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Cefr",
                table: "Cards",
                type: "varchar(2)",
                unicode: false,
                maxLength: 2,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Cards",
                type: "datetime",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DetailsJson",
                table: "Cards",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Emoji",
                table: "Cards",
                type: "varchar(16)",
                maxLength: 16,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ImageSourceJson",
                table: "Cards",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "MeaningEn",
                table: "Cards",
                type: "varchar(400)",
                maxLength: 400,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PartsOfSpeech",
                table: "Cards",
                type: "varchar(120)",
                unicode: false,
                maxLength: 120,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PersonalNote",
                table: "Cards",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PhoneticUk",
                table: "Cards",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Cards",
                type: "datetime",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SetSubscriptions",
                columns: table => new
                {
                    SetSubscriptionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    SetId = table.Column<int>(type: "int", nullable: false),
                    SubscribedAt = table.Column<DateTime>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SetSubscriptions", x => x.SetSubscriptionId);
                    table.ForeignKey(
                        name: "FK_SetSubscriptions_Sets_SetId",
                        column: x => x.SetId,
                        principalTable: "Sets",
                        principalColumn: "SetID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SetSubscriptions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Sets_ShareSlug",
                table: "Sets",
                column: "ShareSlug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SetSubscriptions_SetId",
                table: "SetSubscriptions",
                column: "SetId");

            migrationBuilder.CreateIndex(
                name: "IX_SetSubscriptions_UserId_SetId",
                table: "SetSubscriptions",
                columns: new[] { "UserId", "SetId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Cards_SetID",
                table: "Cards",
                column: "SetID");

            migrationBuilder.DropIndex(
                name: "IX_Cards_SetId_Term",
                table: "Cards");

            migrationBuilder.DropTable(
                name: "SetSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_Sets_ShareSlug",
                table: "Sets");

            migrationBuilder.DropColumn(
                name: "ClonedFromSetId",
                table: "Sets");

            migrationBuilder.DropColumn(
                name: "ShareSlug",
                table: "Sets");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Sets");

            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "Sets");

            migrationBuilder.DropColumn(
                name: "IsSuspended",
                table: "LearningProgress");

            migrationBuilder.DropColumn(
                name: "AiGenerated",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Cefr",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "DetailsJson",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Emoji",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "ImageSourceJson",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "MeaningEn",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "PartsOfSpeech",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "PersonalNote",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "PhoneticUk",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Cards");

        }
    }
}
