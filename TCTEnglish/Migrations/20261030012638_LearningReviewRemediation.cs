using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCTEnglish.Migrations
{
    /// <inheritdoc />
    public partial class LearningReviewRemediation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The replacement keeps its name, so retain FK coverage temporarily.
            migrationBuilder.CreateIndex(
                name: "IX_SpeakingImportJobs_OwnerUserId_MigrationSupport",
                table: "SpeakingImportJobs",
                column: "OwnerUserId");

            migrationBuilder.DropIndex(
                name: "IX_SpeakingImportJobs_OwnerUserId_RequestFingerprint",
                table: "SpeakingImportJobs");

            migrationBuilder.AddColumn<string>(
                name: "LeaseToken",
                table: "SpeakingImportJobs",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "NextAttemptAtUtc",
                table: "SpeakingImportJobs",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceKind",
                table: "SpeakingImportJobs",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SubtitlePayload",
                table: "SpeakingImportJobs",
                type: "longtext",
                maxLength: 1000000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "LastUserId",
                table: "NotificationJobRuns",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeaseToken",
                table: "NotificationJobRuns",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Phase",
                table: "NotificationJobRuns",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "PayloadJson",
                table: "LearningNotificationOutbox",
                type: "varchar(4000)",
                maxLength: 4000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(2000)",
                oldMaxLength: 2000)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "NotificationEventKey",
                table: "LearningNotificationOutbox",
                type: "varchar(160)",
                maxLength: 160,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "OccurredAtUtc",
                table: "LearningNotificationOutbox",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpeakingImportJobs_OwnerUserId_RequestFingerprint",
                table: "SpeakingImportJobs",
                columns: new[] { "OwnerUserId", "RequestFingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LearningNotificationOutbox_UserId_NotificationEventKey",
                table: "LearningNotificationOutbox",
                columns: new[] { "UserId", "NotificationEventKey" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_LearningNotificationOutbox_UserId",
                table: "LearningNotificationOutbox");

            migrationBuilder.DropIndex(
                name: "IX_SpeakingImportJobs_OwnerUserId_MigrationSupport",
                table: "SpeakingImportJobs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SpeakingImportJobs_OwnerUserId_MigrationSupport",
                table: "SpeakingImportJobs",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningNotificationOutbox_UserId",
                table: "LearningNotificationOutbox",
                column: "UserId");

            migrationBuilder.DropIndex(
                name: "IX_SpeakingImportJobs_OwnerUserId_RequestFingerprint",
                table: "SpeakingImportJobs");

            migrationBuilder.DropIndex(
                name: "IX_LearningNotificationOutbox_UserId_NotificationEventKey",
                table: "LearningNotificationOutbox");

            migrationBuilder.DropColumn(
                name: "LeaseToken",
                table: "SpeakingImportJobs");

            migrationBuilder.DropColumn(
                name: "NextAttemptAtUtc",
                table: "SpeakingImportJobs");

            migrationBuilder.DropColumn(
                name: "SourceKind",
                table: "SpeakingImportJobs");

            migrationBuilder.DropColumn(
                name: "SubtitlePayload",
                table: "SpeakingImportJobs");

            migrationBuilder.DropColumn(
                name: "LastUserId",
                table: "NotificationJobRuns");

            migrationBuilder.DropColumn(
                name: "LeaseToken",
                table: "NotificationJobRuns");

            migrationBuilder.DropColumn(
                name: "Phase",
                table: "NotificationJobRuns");

            migrationBuilder.DropColumn(
                name: "NotificationEventKey",
                table: "LearningNotificationOutbox");

            migrationBuilder.DropColumn(
                name: "OccurredAtUtc",
                table: "LearningNotificationOutbox");

            migrationBuilder.AlterColumn<string>(
                name: "PayloadJson",
                table: "LearningNotificationOutbox",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(4000)",
                oldMaxLength: 4000)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_SpeakingImportJobs_OwnerUserId_RequestFingerprint",
                table: "SpeakingImportJobs",
                columns: new[] { "OwnerUserId", "RequestFingerprint" });

            migrationBuilder.DropIndex(
                name: "IX_SpeakingImportJobs_OwnerUserId_MigrationSupport",
                table: "SpeakingImportJobs");
        }
    }
}
