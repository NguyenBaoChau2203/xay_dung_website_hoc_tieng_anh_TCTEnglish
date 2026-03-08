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
            // Skip delete if user #1 has dependent data (Sets, Folders, etc.)
            // This seed row is a real user now — safe to leave in place.
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM [Sets] WHERE [OwnerID] = 1)
                   AND NOT EXISTS (SELECT 1 FROM [Folders] WHERE [UserID] = 1)
                BEGIN
                    DELETE FROM [Users] WHERE [UserID] = 1;
                END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM [Users] WHERE [UserID] = 1)
                    INSERT INTO [Users] ([UserID], [AvatarUrl], [CreatedAt], [Email], [FullName], [Goal], [IsActive], [LastStudyDate], [LongestStreak], [PasswordHash], [ResetPasswordToken], [ResetPasswordTokenExpiry], [Role], [Streak])
                    VALUES (1, NULL, '2026-02-01T16:20:51.117', 'admin@tctenglish.com', 'System Admin', 0, 1, NULL, 0, '$2a$11$P/Ddyz.mGnpom9fEbTcXxuaOmUYMAaCZDKac8vCTJOY6GK4LzYR2y', NULL, NULL, 'Admin', 0);");
        }
    }
}
