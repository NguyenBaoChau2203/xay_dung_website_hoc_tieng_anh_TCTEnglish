using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCTEnglish.Migrations
{
    /// <inheritdoc />
    public partial class AddAiVoiceConversation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiVoiceCapacity",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Revision = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiVoiceCapacity", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AiVoiceCatalogTopics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    StableKey = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Title = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Scenario = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AiRole = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LearnerRole = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Level = table.Column<string>(type: "varchar(2)", maxLength: 2, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsPublished = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiVoiceCatalogTopics", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AiVoiceCustomTopics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Scenario = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AiRole = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LearnerRole = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Level = table.Column<string>(type: "varchar(2)", maxLength: 2, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiVoiceCustomTopics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiVoiceCustomTopics_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AiVoiceSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    TopicKind = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TopicId = table.Column<int>(type: "int", nullable: false),
                    TopicTitle = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TopicSnapshot = table.Column<string>(type: "varchar(6000)", maxLength: 6000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Model = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    State = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AgentState = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProviderCallId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AccountingState = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    QuotaDayUtc = table.Column<DateTime>(type: "date", nullable: false),
                    ReservedSeconds = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastActivityAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    HeartbeatAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CloseReason = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProviderDispatched = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SetupFinished = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CancelRequested = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    HistoryDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    NeedsHangup = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    HangupAttempts = table.Column<int>(type: "int", nullable: false),
                    NextCleanupAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CleanupClaim = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CleanupClaimUntilUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    PlayingResponseId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiVoiceSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiVoiceSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AiVoiceUsageDays",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    UtcDate = table.Column<DateTime>(type: "date", nullable: false),
                    ReservedSeconds = table.Column<int>(type: "int", nullable: false),
                    ConsumedSeconds = table.Column<int>(type: "int", nullable: false),
                    InputAudioTokens = table.Column<long>(type: "bigint", nullable: false),
                    OutputAudioTokens = table.Column<long>(type: "bigint", nullable: false),
                    InputTextTokens = table.Column<long>(type: "bigint", nullable: false),
                    OutputTextTokens = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiVoiceUsageDays", x => new { x.UserId, x.UtcDate });
                    table.ForeignKey(
                        name: "FK_AiVoiceUsageDays_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AiVoiceReceipts",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Kind = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EventKey = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiVoiceReceipts", x => new { x.SessionId, x.Kind, x.EventKey });
                    table.ForeignKey(
                        name: "FK_AiVoiceReceipts_AiVoiceSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "AiVoiceSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AiVoiceTurns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    SessionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ItemId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ResponseId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Speaker = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Text = table.Column<string>(type: "varchar(12000)", maxLength: 12000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Interrupted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiVoiceTurns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiVoiceTurns_AiVoiceSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "AiVoiceSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AiVoiceUserLeases",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    SessionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiVoiceUserLeases", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_AiVoiceUserLeases_AiVoiceSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "AiVoiceSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AiVoiceUserLeases_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "AiVoiceCapacity",
                columns: new[] { "Id", "Revision" },
                values: new object[] { 1, 0 });

            migrationBuilder.CreateIndex(
                name: "IX_AiVoiceCatalogTopics_IsPublished_SortOrder",
                table: "AiVoiceCatalogTopics",
                columns: new[] { "IsPublished", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_AiVoiceCatalogTopics_StableKey",
                table: "AiVoiceCatalogTopics",
                column: "StableKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiVoiceCustomTopics_UserId_IsDeleted",
                table: "AiVoiceCustomTopics",
                columns: new[] { "UserId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_AiVoiceSessions_State_NextCleanupAtUtc",
                table: "AiVoiceSessions",
                columns: new[] { "State", "NextCleanupAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AiVoiceSessions_UserId_CreatedAtUtc",
                table: "AiVoiceSessions",
                columns: new[] { "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AiVoiceTurns_SessionId_ItemId_Speaker",
                table: "AiVoiceTurns",
                columns: new[] { "SessionId", "ItemId", "Speaker" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiVoiceUserLeases_SessionId",
                table: "AiVoiceUserLeases",
                column: "SessionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiVoiceCapacity");

            migrationBuilder.DropTable(
                name: "AiVoiceCatalogTopics");

            migrationBuilder.DropTable(
                name: "AiVoiceCustomTopics");

            migrationBuilder.DropTable(
                name: "AiVoiceReceipts");

            migrationBuilder.DropTable(
                name: "AiVoiceTurns");

            migrationBuilder.DropTable(
                name: "AiVoiceUsageDays");

            migrationBuilder.DropTable(
                name: "AiVoiceUserLeases");

            migrationBuilder.DropTable(
                name: "AiVoiceSessions");
        }
    }
}
