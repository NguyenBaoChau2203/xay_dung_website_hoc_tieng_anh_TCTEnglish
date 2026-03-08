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
            // Idempotent: only drop FK if it still exists
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Users_Classes_ClassId')
                    ALTER TABLE [Users] DROP CONSTRAINT [FK_Users_Classes_ClassId];");

            // Idempotent: only drop index if it still exists
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_ClassId' AND object_id = OBJECT_ID('Users'))
                    DROP INDEX [IX_Users_ClassId] ON [Users];");

            // Idempotent: only drop column if it still exists
            migrationBuilder.Sql(@"
                IF COL_LENGTH('Users', 'ClassId') IS NOT NULL
                BEGIN
                    DECLARE @df NVARCHAR(256);
                    SELECT @df = QUOTENAME(d.name)
                    FROM sys.default_constraints d
                    JOIN sys.columns c ON d.parent_column_id = c.column_id AND d.parent_object_id = c.object_id
                    WHERE d.parent_object_id = OBJECT_ID('Users') AND c.name = 'ClassId';
                    IF @df IS NOT NULL EXEC('ALTER TABLE [Users] DROP CONSTRAINT ' + @df);
                    ALTER TABLE [Users] DROP COLUMN [ClassId];
                END");

            // Idempotent: only add IsActive if it doesn't already exist
            migrationBuilder.Sql(@"
                IF COL_LENGTH('Users', 'IsActive') IS NULL
                    ALTER TABLE [Users] ADD [IsActive] BIT NOT NULL DEFAULT CAST(1 AS BIT);");

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

            // Idempotent: update seed data only if row exists
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM [Users] WHERE [UserID] = 1)
                    UPDATE [Users]
                    SET [Email] = 'admin@tctenglish.com',
                        [FullName] = 'System Admin',
                        [IsActive] = 1,
                        [PasswordHash] = '$2a$11$P/Ddyz.mGnpom9fEbTcXxuaOmUYMAaCZDKac8vCTJOY6GK4LzYR2y',
                        [Role] = 'Admin'
                    WHERE [UserID] = 1;");

            // Re-add FK (Restrict) — idempotent via DROP + ADD
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK__Folders__ParentF__3E52440B')
                    ALTER TABLE [Folders] ADD CONSTRAINT [FK__Folders__ParentF__3E52440B]
                        FOREIGN KEY ([ParentFolderID]) REFERENCES [Folders]([FolderID]);");
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
