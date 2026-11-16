using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TCTEnglish.Migrations
{
    /// <inheritdoc />
    public partial class AddBadgeTiersAndDiscoveryAwards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MariaDB validates catalog identity before any auto-committing DDL. Never overwrite custom rows.
            migrationBuilder.Sql("""
                BEGIN NOT ATOMIC
                    IF EXISTS (SELECT 1 FROM Badges WHERE Id BETWEEN 13 AND 22 OR Code IN ('writing-mood', 'study-checkin', 'writing-deadline', 'xp-mood', 'keyboard-form', 'steady-flame', 'keyboard-boss', 'monthly-flame', 'secret-plot-twist', 'secret-quiet-effort'))
                       OR EXISTS (SELECT 1 FROM (SELECT 1 AS Id, 'first-session' AS Code UNION ALL SELECT 2 AS Id, 'three-day-streak' AS Code UNION ALL SELECT 3 AS Id, 'seven-day-peak' AS Code UNION ALL SELECT 4 AS Id, 'active-week' AS Code UNION ALL SELECT 5 AS Id, 'xp-collector' AS Code UNION ALL SELECT 6 AS Id, 'xp-champion' AS Code UNION ALL SELECT 7 AS Id, 'speaking-first-video' AS Code UNION ALL SELECT 8 AS Id, 'speaking-five-videos' AS Code UNION ALL SELECT 9 AS Id, 'vocabulary-first-mastered' AS Code UNION ALL SELECT 10 AS Id, 'vocabulary-ten-mastered' AS Code UNION ALL SELECT 11 AS Id, 'writing-first-exercise' AS Code UNION ALL SELECT 12 AS Id, 'writing-five-exercises' AS Code) expected
                           LEFT JOIN Badges actual ON actual.Id = expected.Id AND BINARY actual.Code = BINARY expected.Code
                           WHERE actual.Id IS NULL)
                    THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Badge catalog identity conflict; resolve before upgrade';
                    END IF;
                END
                """);

            migrationBuilder.AddColumn<bool>(
                name: "IsRetroactive",
                table: "UserBadges",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DifficultyKey",
                table: "Badges",
                type: "varchar(30)",
                maxLength: 30,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "IsSecret",
                table: "Badges",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "Badges",
                keyColumn: "Id",
                keyValue: 1,
                column: "DifficultyKey",
                value: "chill");

            migrationBuilder.UpdateData(
                table: "Badges",
                keyColumn: "Id",
                keyValue: 2,
                column: "DifficultyKey",
                value: "cuon");

            migrationBuilder.UpdateData(
                table: "Badges",
                keyColumn: "Id",
                keyValue: 3,
                column: "DifficultyKey",
                value: "cuon");

            migrationBuilder.UpdateData(
                table: "Badges",
                keyColumn: "Id",
                keyValue: 4,
                column: "DifficultyKey",
                value: "cuon");

            migrationBuilder.UpdateData(
                table: "Badges",
                keyColumn: "Id",
                keyValue: 5,
                column: "DifficultyKey",
                value: "chill");

            migrationBuilder.UpdateData(
                table: "Badges",
                keyColumn: "Id",
                keyValue: 6,
                column: "DifficultyKey",
                value: "cuon");

            migrationBuilder.UpdateData(
                table: "Badges",
                keyColumn: "Id",
                keyValue: 7,
                column: "DifficultyKey",
                value: "chill");

            migrationBuilder.UpdateData(
                table: "Badges",
                keyColumn: "Id",
                keyValue: 8,
                column: "DifficultyKey",
                value: "cuon");

            migrationBuilder.UpdateData(
                table: "Badges",
                keyColumn: "Id",
                keyValue: 9,
                column: "DifficultyKey",
                value: "chill");

            migrationBuilder.UpdateData(
                table: "Badges",
                keyColumn: "Id",
                keyValue: 10,
                column: "DifficultyKey",
                value: "cuon");

            migrationBuilder.UpdateData(
                table: "Badges",
                keyColumn: "Id",
                keyValue: 11,
                column: "DifficultyKey",
                value: "chill");

            migrationBuilder.UpdateData(
                table: "Badges",
                keyColumn: "Id",
                keyValue: 12,
                column: "DifficultyKey",
                value: "cuon");

            migrationBuilder.InsertData(
                table: "Badges",
                columns: new[] { "Id", "Code", "Description", "DifficultyKey", "IconClass", "MetricType", "Name", "SortOrder", "ThresholdValue" },
                values: new object[,]
                {
                    { 13, "writing-mood", "Hoàn thành lần đầu 3 bài Writing khác nhau.", "chill", "fas fa-award", 8, "Bút mới lên mood", 13, 3 },
                    { 14, "study-checkin", "Có 3 ngày học đã xác nhận.", "chill", "fas fa-award", 3, "Check-in bằng bài học", 14, 3 },
                    { 15, "writing-deadline", "Hoàn thành lần đầu 10 bài Writing khác nhau.", "cuon", "fas fa-award", 8, "Deadline cũng phải nể", 15, 10 },
                    { 16, "xp-mood", "Tích lũy 500 XP từ hoạt động học tập.", "cuon", "fas fa-award", 4, "XP lên mood", 16, 500 },
                    { 17, "keyboard-form", "Hoàn thành lần đầu 25 bài Writing khác nhau.", "chien-than", "fas fa-award", 8, "Bàn phím vào form", 17, 25 },
                    { 18, "steady-flame", "Có chuỗi 14 ngày học liên tiếp đã xác nhận.", "chien-than", "fas fa-award", 9, "Giữ lửa không seen", 18, 14 },
                    { 19, "keyboard-boss", "Hoàn thành lần đầu 50 bài Writing khác nhau.", "trum-cuoi", "fas fa-award", 8, "Trùm cuối bàn phím", 19, 50 },
                    { 20, "monthly-flame", "Có chuỗi 30 ngày học liên tiếp đã xác nhận.", "trum-cuoi", "fas fa-award", 9, "Giữ lửa xuyên tháng", 20, 30 }
                });

            migrationBuilder.InsertData(
                table: "Badges",
                columns: new[] { "Id", "Code", "Description", "DifficultyKey", "IconClass", "IsSecret", "MetricType", "Name", "SortOrder", "ThresholdValue" },
                values: new object[,]
                {
                    { 21, "secret-plot-twist", "Hoàn thành lần đầu 13 bài Writing khác nhau.", "chien-than", "fas fa-award", true, 8, "Plot twist thứ 13", 21, 13 },
                    { 22, "secret-quiet-effort", "Có 21 ngày học đã xác nhận.", "chien-than", "fas fa-award", true, 3, "Hệ chăm ngầm", 22, 21 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A downgrade must never cascade-delete earned history.
            migrationBuilder.Sql("""
                BEGIN NOT ATOMIC
                    IF EXISTS (SELECT 1 FROM UserBadges WHERE BadgeID BETWEEN 13 AND 22)
                    THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Earned discovery awards prevent downgrade';
                    END IF;
                END
                """);
            migrationBuilder.DeleteData(
                table: "Badges",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "Badges",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "Badges",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "Badges",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "Badges",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "Badges",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "Badges",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "Badges",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "Badges",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "Badges",
                keyColumn: "Id",
                keyValue: 22);

            migrationBuilder.DropColumn(
                name: "IsRetroactive",
                table: "UserBadges");

            migrationBuilder.DropColumn(
                name: "DifficultyKey",
                table: "Badges");

            migrationBuilder.DropColumn(
                name: "IsSecret",
                table: "Badges");
        }
    }
}
