using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TCTVocabulary.Migrations
{
    /// <inheritdoc />
    public partial class AddGoalsBadges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Badges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IconClass = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MetricType = table.Column<int>(type: "int", nullable: false),
                    ThresholdValue = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Badges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserBadges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    BadgeID = table.Column<int>(type: "int", nullable: false),
                    AwardedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserBadges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserBadges_Badges",
                        column: x => x.BadgeID,
                        principalTable: "Badges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserBadges_Users",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Badges",
                columns: new[] { "Id", "Code", "Description", "IconClass", "MetricType", "Name", "SortOrder", "ThresholdValue" },
                values: new object[,]
                {
                    { 1, "first-session", "Hoàn thành ngày học đầu tiên để bắt đầu hành trình.", "fas fa-seedling", 3, "Khởi động", 1, 1 },
                    { 2, "three-day-streak", "Duy trì streak học tập trong 3 ngày liên tiếp.", "fas fa-fire", 1, "Giữ nhịp", 2, 3 },
                    { 3, "seven-day-peak", "Chạm mốc streak dài nhất 7 ngày.", "fas fa-bolt", 2, "Bền bỉ", 3, 7 },
                    { 4, "active-week", "Có hoạt động học tập trong 7 ngày khác nhau.", "fas fa-calendar-check", 3, "Cả tuần chăm chỉ", 4, 7 },
                    { 5, "xp-collector", "Tích lũy đủ 50 XP từ các hoạt động học tập.", "fas fa-star", 4, "Tích điểm", 5, 50 },
                    { 6, "xp-champion", "Đạt 200 XP để mở khóa cột mốc cao hơn.", "fas fa-trophy", 4, "Bứt phá", 6, 200 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Badges_Code",
                table: "Badges",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserBadges_BadgeID",
                table: "UserBadges",
                column: "BadgeID");

            migrationBuilder.CreateIndex(
                name: "IX_UserBadges_UserId_BadgeId",
                table: "UserBadges",
                columns: new[] { "UserID", "BadgeID" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserBadges");

            migrationBuilder.DropTable(
                name: "Badges");
        }
    }
}
