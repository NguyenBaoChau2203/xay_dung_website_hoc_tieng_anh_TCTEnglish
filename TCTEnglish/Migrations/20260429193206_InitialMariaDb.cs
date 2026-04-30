using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TCTEnglish.Migrations
{
    /// <inheritdoc />
    public partial class InitialMariaDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Badges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Code = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IconClass = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MetricType = table.Column<int>(type: "int", nullable: false),
                    ThresholdValue = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Badges", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PremiumPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PriceVnd = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DurationDays = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PremiumPlans", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ReadingPassages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Title = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Content = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Level = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Topic = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ImageUrl = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsPublished = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadingPassages", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SpeakingPlaylists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ThumbnailUrl = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpeakingPlaylists", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Email = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PasswordHash = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FullName = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Role = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    AvatarUrl = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ResetPasswordToken = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ResetPasswordTokenExpiry = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LockReason = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LockExpiry = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Streak = table.Column<int>(type: "int", nullable: true, defaultValue: 0),
                    LongestStreak = table.Column<int>(type: "int", nullable: true, defaultValue: 0),
                    LastStudyDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Goal = table.Column<int>(type: "int", nullable: true, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(CURRENT_TIMESTAMP)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Users__1788CCAC46CDCFCA", x => x.UserID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ReadingQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    PassageId = table.Column<int>(type: "int", nullable: false),
                    QuestionText = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OrderIndex = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadingQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReadingQuestions_ReadingPassages_PassageId",
                        column: x => x.PassageId,
                        principalTable: "ReadingPassages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AiConversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "(UTC_TIMESTAMP(6))"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "(UTC_TIMESTAMP(6))")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiConversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiConversations_Users",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Classes",
                columns: table => new
                {
                    ClassID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ClassName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OwnerID = table.Column<int>(type: "int", nullable: false),
                    PasswordHash = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HasPassword = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    ImageUrl = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(CURRENT_TIMESTAMP)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Classes__CB1927A0B52422EC", x => x.ClassID);
                    table.ForeignKey(
                        name: "FK__Classes__OwnerID__412EB0B6",
                        column: x => x.OwnerID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Folders",
                columns: table => new
                {
                    FolderID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    FolderName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ParentFolderID = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Folders__ACD7109F2ECFF2AF", x => x.FolderID);
                    table.ForeignKey(
                        name: "FK__Folders__ParentF__3E52440B",
                        column: x => x.ParentFolderID,
                        principalTable: "Folders",
                        principalColumn: "FolderID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK__Folders__UserID__3D5E1FD2",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ListeningLessons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Title = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Level = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Topic = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    YoutubeId = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AudioUrl = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ThumbnailUrl = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Duration = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Speaker1Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Speaker2Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Speaker1Country = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Speaker2Country = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsPublished = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    OwnerUserId = table.Column<int>(type: "int", nullable: true),
                    TranscriptSource = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "(UTC_TIMESTAMP(6))")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListeningLessons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ListeningLessons_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "UserID");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Message = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsRead = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    RelatedUrl = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IconClass = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "(UTC_TIMESTAMP(6))")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Users",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PaymentOrders",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    OrderCode = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    PlanId = table.Column<int>(type: "int", nullable: false),
                    Provider = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AmountVnd = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RawStatus = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ConfirmedByUserId = table.Column<int>(type: "int", nullable: true),
                    ActivatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ProviderTransactionId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProviderRequestId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProviderPaymentUrl = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProviderDeepLink = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProviderQrCodePayload = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProviderResponseCode = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProviderTransactionStatus = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BankCode = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BankTransactionNo = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CardType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PayType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FailureMessage = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReturnPayloadJson = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IpnPayloadJson = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentOrders_PremiumPlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "PremiumPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentOrders_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SpeakingVideos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    PlaylistId = table.Column<int>(type: "int", nullable: true),
                    OwnerUserId = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    YoutubeId = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Level = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Topic = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ThumbnailUrl = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Duration = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceUrl = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, defaultValue: "admin")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TranscriptSource = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ImportStatus = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, defaultValue: "ready")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "(UTC_TIMESTAMP(6))")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpeakingVideos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpeakingVideos_SpeakingPlaylists",
                        column: x => x.PlaylistId,
                        principalTable: "SpeakingPlaylists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SpeakingVideos_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "UserID");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UserBadges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    BadgeID = table.Column<int>(type: "int", nullable: false),
                    AwardedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "(UTC_TIMESTAMP(6))")
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UserDailyActivities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    ActivityDate = table.Column<DateTime>(type: "date", nullable: false),
                    XpEarned = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    StreakXpAwarded = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CardsReviewed = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    NewCardsLearned = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    VocabularyCompletedCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    QuizzesCompleted = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    SpeakingCompletedCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    WritingCompletedCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ReadingCompletedCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ListeningCompletedCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDailyActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserDailyActivities_Users",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UserGoals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    GoalArea = table.Column<int>(type: "int", nullable: false),
                    TargetValue = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "(UTC_TIMESTAMP(6))"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "(UTC_TIMESTAMP(6))")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserGoals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserGoals_Users",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UserReadingHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ReadingPassageId = table.Column<int>(type: "int", nullable: false),
                    ViewedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IsCompleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserReadingHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserReadingHistories_ReadingPassages_ReadingPassageId",
                        column: x => x.ReadingPassageId,
                        principalTable: "ReadingPassages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserReadingHistories_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "WritingExercises",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserID = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Level = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ContentType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Topic = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, defaultValue: "admin")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PreviewText = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsPublished = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "(UTC_TIMESTAMP(6))")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WritingExercises", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WritingExercises_Users",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "WritingGenerationLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    RequestType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsSuccess = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ErrorCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "(UTC_TIMESTAMP(6))")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WritingGenerationLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WritingGenerationLogs_Users",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ReadingOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    QuestionId = table.Column<int>(type: "int", nullable: false),
                    OptionText = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsCorrect = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadingOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReadingOptions_ReadingQuestions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "ReadingQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AiMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ConversationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Role = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "varchar(8000)", maxLength: 8000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PromptTokens = table.Column<int>(type: "int", nullable: true),
                    CompletionTokens = table.Column<int>(type: "int", nullable: true),
                    ModelName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "(UTC_TIMESTAMP(6))")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiMessages_AiConversations",
                        column: x => x.ConversationId,
                        principalTable: "AiConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AiRequestLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    ConversationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    IsSuccess = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ErrorCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LatencyMs = table.Column<int>(type: "int", nullable: true),
                    PromptTokens = table.Column<int>(type: "int", nullable: true),
                    CompletionTokens = table.Column<int>(type: "int", nullable: true),
                    TotalTokens = table.Column<int>(type: "int", nullable: true),
                    ModelName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "(UTC_TIMESTAMP(6))")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiRequestLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiRequestLogs_AiConversations",
                        column: x => x.ConversationId,
                        principalTable: "AiConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AiRequestLogs_Users",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ClassMembers",
                columns: table => new
                {
                    UserID = table.Column<int>(type: "int", nullable: false),
                    ClassID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassMembers", x => new { x.ClassID, x.UserID });
                    table.ForeignKey(
                        name: "FK_ClassMembers_Classes_ClassID",
                        column: x => x.ClassID,
                        principalTable: "Classes",
                        principalColumn: "ClassID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassMembers_Users_UserID",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ClassMessages",
                columns: table => new
                {
                    MessageId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ClassId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassMessages", x => x.MessageId);
                    table.ForeignKey(
                        name: "FK_ClassMessages_Classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "ClassID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassMessages_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ClassFolders",
                columns: table => new
                {
                    ClassFolderID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ClassID = table.Column<int>(type: "int", nullable: false),
                    FolderID = table.Column<int>(type: "int", nullable: false),
                    AddedByUserID = table.Column<int>(type: "int", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "(UTC_TIMESTAMP)")
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SavedFolders",
                columns: table => new
                {
                    SavedFolderId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    FolderId = table.Column<int>(type: "int", nullable: false),
                    SavedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedFolders", x => x.SavedFolderId);
                    table.ForeignKey(
                        name: "FK_SavedFolders_Folders_FolderId",
                        column: x => x.FolderId,
                        principalTable: "Folders",
                        principalColumn: "FolderID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SavedFolders_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Sets",
                columns: table => new
                {
                    SetID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    SetName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OwnerID = table.Column<int>(type: "int", nullable: false),
                    FolderID = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(CURRENT_TIMESTAMP)"),
                    Description = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ViewCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ListeningQuizQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    LessonId = table.Column<int>(type: "int", nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false),
                    QuestionText = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OptionA = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OptionB = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OptionC = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OptionD = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CorrectAnswer = table.Column<string>(type: "varchar(1)", maxLength: 1, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Explanation = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListeningQuizQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ListeningQuizQuestions_ListeningLessons",
                        column: x => x.LessonId,
                        principalTable: "ListeningLessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ListeningTranscriptLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    LessonId = table.Column<int>(type: "int", nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false),
                    Speaker = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Text = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    VietnameseMeaning = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StartTime = table.Column<double>(type: "double", nullable: true),
                    EndTime = table.Column<double>(type: "double", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListeningTranscriptLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ListeningTranscriptLines_ListeningLessons",
                        column: x => x.LessonId,
                        principalTable: "ListeningLessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ListeningVocabItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    LessonId = table.Column<int>(type: "int", nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false),
                    Word = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Definition = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExampleSentence = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ImageUrl = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListeningVocabItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ListeningVocabItems_ListeningLessons",
                        column: x => x.LessonId,
                        principalTable: "ListeningLessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UserListeningProgresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    LessonId = table.Column<int>(type: "int", nullable: false),
                    TranscriptCompleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    QuizCompleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    QuizScore = table.Column<int>(type: "int", nullable: true),
                    VocabReviewed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastAccessedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "(UTC_TIMESTAMP(6))")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserListeningProgresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserListeningProgress_ListeningLessons",
                        column: x => x.LessonId,
                        principalTable: "ListeningLessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserListeningProgress_Users",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PaymentEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Provider = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EventType = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EventKey = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PaymentOrderId = table.Column<long>(type: "bigint", nullable: true),
                    SignatureValid = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ResultCode = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PayloadJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProcessingStatus = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProcessingMessage = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentEvents_PaymentOrders_PaymentOrderId",
                        column: x => x.PaymentOrderId,
                        principalTable: "PaymentOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UserSubscriptions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    PlanId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StartsAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EndsAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ActivatedByPaymentOrderId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CancelReason = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSubscriptions_PaymentOrders_ActivatedByPaymentOrderId",
                        column: x => x.ActivatedByPaymentOrderId,
                        principalTable: "PaymentOrders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserSubscriptions_PremiumPlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "PremiumPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserSubscriptions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SpeakingSentences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    VideoId = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<double>(type: "double", nullable: false),
                    EndTime = table.Column<double>(type: "double", nullable: false),
                    Text = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    VietnameseMeaning = table.Column<string>(type: "longtext", nullable: false, defaultValue: "")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpeakingSentences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpeakingSentences_SpeakingVideos",
                        column: x => x.VideoId,
                        principalTable: "SpeakingVideos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UserSpeakingVideoCompletions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    VideoID = table.Column<int>(type: "int", nullable: false),
                    CompletedSentenceCount = table.Column<int>(type: "int", nullable: false),
                    RequiredSentenceCount = table.Column<int>(type: "int", nullable: false),
                    IsCompleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastEvaluatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "(UTC_TIMESTAMP(6))")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSpeakingVideoCompletions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSpeakingVideoCompletions_SpeakingVideos",
                        column: x => x.VideoID,
                        principalTable: "SpeakingVideos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserSpeakingVideoCompletions_Users",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UserWritingExerciseProgresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    WritingExerciseID = table.Column<int>(type: "int", nullable: false),
                    TotalSentenceCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    PassedSentenceCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    AttemptCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsCompleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    LastAttemptAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserWritingExerciseProgresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserWritingExerciseProgresses_Users",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserWritingExerciseProgresses_WritingExercises",
                        column: x => x.WritingExerciseID,
                        principalTable: "WritingExercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "WritingExerciseSentences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    WritingExerciseId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    VietnameseText = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EnglishMeaning = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BreakAfter = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WritingExerciseSentences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WritingExerciseSentences_WritingExercises",
                        column: x => x.WritingExerciseId,
                        principalTable: "WritingExercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Cards",
                columns: table => new
                {
                    CardID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    SetID = table.Column<int>(type: "int", nullable: false),
                    Term = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Definition = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ImageUrl = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Phonetic = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Example = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExampleTranslation = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Topic = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Cards__55FECD8E2103FA66", x => x.CardID);
                    table.ForeignKey(
                        name: "FK__Cards__SetID__4CA06362",
                        column: x => x.SetID,
                        principalTable: "Sets",
                        principalColumn: "SetID");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PaymentAdminActions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    PaymentOrderId = table.Column<long>(type: "bigint", nullable: true),
                    SubscriptionId = table.Column<long>(type: "bigint", nullable: true),
                    AdminUserId = table.Column<int>(type: "int", nullable: false),
                    ActionType = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Reason = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OldStatus = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NewStatus = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PayloadJson = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IpAddress = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserAgent = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAdminActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentAdminActions_PaymentOrders_PaymentOrderId",
                        column: x => x.PaymentOrderId,
                        principalTable: "PaymentOrders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PaymentAdminActions_UserSubscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "UserSubscriptions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PaymentAdminActions_Users_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "Users",
                        principalColumn: "UserID");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UserSpeakingProgresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    SentenceId = table.Column<int>(type: "int", nullable: false),
                    TotalScore = table.Column<double>(type: "double", nullable: false),
                    AccuracyScore = table.Column<double>(type: "double", nullable: false),
                    FluencyScore = table.Column<double>(type: "double", nullable: false),
                    CompletenessScore = table.Column<double>(type: "double", nullable: false),
                    PracticedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "(CURRENT_TIMESTAMP)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSpeakingProgresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSpeakingProgress_SpeakingSentences",
                        column: x => x.SentenceId,
                        principalTable: "SpeakingSentences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserSpeakingProgress_Users",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UserWritingAttempts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    WritingExerciseId = table.Column<int>(type: "int", nullable: false),
                    WritingExerciseSentenceId = table.Column<int>(type: "int", nullable: false),
                    SubmittedAnswer = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Passed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    UsedAi = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    EvaluationSource = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SummaryTitle = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SummaryText = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReviewText = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MeaningFeedback = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GrammarFeedback = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NaturalnessFeedback = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    WordChoiceFeedback = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SuggestedRewrite = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "(UTC_TIMESTAMP(6))")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserWritingAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserWritingAttempts_Users",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserWritingAttempts_WritingExerciseSentences",
                        column: x => x.WritingExerciseSentenceId,
                        principalTable: "WritingExerciseSentences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserWritingAttempts_WritingExercises",
                        column: x => x.WritingExerciseId,
                        principalTable: "WritingExercises",
                        principalColumn: "Id");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UserWritingSentenceProgresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    WritingExerciseID = table.Column<int>(type: "int", nullable: false),
                    SentenceID = table.Column<int>(type: "int", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsPassed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AcceptedAnswer = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastAttemptAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    PassedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserWritingSentenceProgresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserWritingSentenceProgresses_Users",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserWritingSentenceProgresses_WritingExerciseSentences",
                        column: x => x.SentenceID,
                        principalTable: "WritingExerciseSentences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserWritingSentenceProgresses_WritingExercises",
                        column: x => x.WritingExerciseID,
                        principalTable: "WritingExercises",
                        principalColumn: "Id");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "LearningProgress",
                columns: table => new
                {
                    ProgressID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    CardID = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    WrongCount = table.Column<int>(type: "int", nullable: true, defaultValue: 0),
                    RepetitionCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LastReviewedDate = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(CURRENT_TIMESTAMP)"),
                    NextReviewDate = table.Column<DateTime>(type: "datetime", nullable: true)
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

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
                    { 6, "xp-champion", "Đạt 200 XP để mở khóa cột mốc cao hơn.", "fas fa-trophy", 4, "Bứt phá", 6, 200 },
                    { 7, "speaking-first-video", "Hoàn thành video Speaking đầu tiên.", "fas fa-microphone", 5, "Khởi động Speaking", 7, 1 },
                    { 8, "speaking-five-videos", "Hoàn thành 5 video Speaking để duy trì luyện tập.", "fas fa-comments", 5, "Nói trôi chảy", 8, 5 },
                    { 9, "vocabulary-first-mastered", "Hoàn thành 1 thẻ Vocabulary ở trạng thái Mastered.", "fas fa-book-open", 6, "Mở khóa Từ vựng", 9, 1 },
                    { 10, "vocabulary-ten-mastered", "Hoàn thành 10 thẻ Vocabulary ở trạng thái Mastered.", "fas fa-spell-check", 6, "Nhịp từ vựng", 10, 10 },
                    { 11, "writing-first-exercise", "Hoàn thành bài Writing đầu tiên.", "fas fa-pen", 7, "Mở khóa Writing", 11, 1 },
                    { 12, "writing-five-exercises", "Hoàn thành 5 bài Writing để duy trì thói quen luyện viết.", "fas fa-pen-fancy", 7, "Viết chắc tay", 12, 5 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiConversations_UserId_UpdatedAtUtc",
                table: "AiConversations",
                columns: new[] { "UserID", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AiMessages_ConversationId_CreatedAtUtc",
                table: "AiMessages",
                columns: new[] { "ConversationId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AiRequestLogs_ConversationId",
                table: "AiRequestLogs",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_AiRequestLogs_RequestedAtUtc",
                table: "AiRequestLogs",
                column: "RequestedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AiRequestLogs_UserId_RequestedAtUtc",
                table: "AiRequestLogs",
                columns: new[] { "UserID", "RequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Badges_Code",
                table: "Badges",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cards_SetID",
                table: "Cards",
                column: "SetID");

            migrationBuilder.CreateIndex(
                name: "IX_Classes_OwnerID",
                table: "Classes",
                column: "OwnerID");

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

            migrationBuilder.CreateIndex(
                name: "IX_ClassMembers_UserID",
                table: "ClassMembers",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_ClassMessages_ClassId",
                table: "ClassMessages",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassMessages_UserId",
                table: "ClassMessages",
                column: "UserId");

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
                name: "IX_ListeningLessons_OwnerUserId_CreatedAt",
                table: "ListeningLessons",
                columns: new[] { "OwnerUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ListeningLessons_Published_Level_Topic",
                table: "ListeningLessons",
                columns: new[] { "IsPublished", "Level", "Topic" });

            migrationBuilder.CreateIndex(
                name: "IX_ListeningQuizQuestions_LessonId_OrderIndex",
                table: "ListeningQuizQuestions",
                columns: new[] { "LessonId", "OrderIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ListeningTranscriptLines_LessonId_OrderIndex",
                table: "ListeningTranscriptLines",
                columns: new[] { "LessonId", "OrderIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ListeningVocabItems_LessonId_OrderIndex",
                table: "ListeningVocabItems",
                columns: new[] { "LessonId", "OrderIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IsRead_CreatedAt",
                table: "Notifications",
                columns: new[] { "UserId", "IsRead", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAdminActions_AdminUserId",
                table: "PaymentAdminActions",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAdminActions_CreatedAtUtc",
                table: "PaymentAdminActions",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAdminActions_PaymentOrderId",
                table: "PaymentAdminActions",
                column: "PaymentOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAdminActions_SubscriptionId",
                table: "PaymentAdminActions",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEvents_PaymentOrderId",
                table: "PaymentEvents",
                column: "PaymentOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEvents_Provider_EventType_EventKey",
                table: "PaymentEvents",
                columns: new[] { "Provider", "EventType", "EventKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentOrders_OrderCode",
                table: "PaymentOrders",
                column: "OrderCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentOrders_PlanId",
                table: "PaymentOrders",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentOrders_UserId_Status_CreatedAt",
                table: "PaymentOrders",
                columns: new[] { "UserId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PremiumPlans_Code",
                table: "PremiumPlans",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReadingOptions_QuestionId",
                table: "ReadingOptions",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingQuestions_PassageId",
                table: "ReadingQuestions",
                column: "PassageId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedFolders_FolderId",
                table: "SavedFolders",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedFolders_UserId_FolderId",
                table: "SavedFolders",
                columns: new[] { "UserId", "FolderId" },
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
                name: "IX_SpeakingSentences_VideoId_StartTime",
                table: "SpeakingSentences",
                columns: new[] { "VideoId", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_SpeakingVideos_OwnerUserId_CreatedAt",
                table: "SpeakingVideos",
                columns: new[] { "OwnerUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SpeakingVideos_OwnerUserId_YoutubeId",
                table: "SpeakingVideos",
                columns: new[] { "OwnerUserId", "YoutubeId" },
                unique: true,
                filter: "(`OwnerUserId` IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_SpeakingVideos_PlaylistId",
                table: "SpeakingVideos",
                column: "PlaylistId");

            migrationBuilder.CreateIndex(
                name: "IX_UserBadges_BadgeID",
                table: "UserBadges",
                column: "BadgeID");

            migrationBuilder.CreateIndex(
                name: "IX_UserBadges_UserId_BadgeId",
                table: "UserBadges",
                columns: new[] { "UserID", "BadgeID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserDailyActivities_UserId_ActivityDate",
                table: "UserDailyActivities",
                columns: new[] { "UserID", "ActivityDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserGoals_UserId_GoalArea",
                table: "UserGoals",
                columns: new[] { "UserID", "GoalArea" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserListeningProgress_UserId_LessonId",
                table: "UserListeningProgresses",
                columns: new[] { "UserId", "LessonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserListeningProgresses_LessonId",
                table: "UserListeningProgresses",
                column: "LessonId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReadingHistories_ReadingPassageId",
                table: "UserReadingHistories",
                column: "ReadingPassageId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReadingHistories_UserId_ReadingPassageId",
                table: "UserReadingHistories",
                columns: new[] { "UserId", "ReadingPassageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ__Users__A9D10534362A84D8",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSpeakingProgresses_SentenceId",
                table: "UserSpeakingProgresses",
                column: "SentenceId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSpeakingProgresses_UserId",
                table: "UserSpeakingProgresses",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSpeakingVideoCompletions_UserId_VideoId",
                table: "UserSpeakingVideoCompletions",
                columns: new[] { "UserID", "VideoID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSpeakingVideoCompletions_VideoID",
                table: "UserSpeakingVideoCompletions",
                column: "VideoID");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_ActivatedByPaymentOrderId",
                table: "UserSubscriptions",
                column: "ActivatedByPaymentOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_PlanId",
                table: "UserSubscriptions",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_UserId_Status_EndsAtUtc",
                table: "UserSubscriptions",
                columns: new[] { "UserId", "Status", "EndsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserWritingAttempts_UserId_WritingExerciseId_CreatedAtUtc",
                table: "UserWritingAttempts",
                columns: new[] { "UserID", "WritingExerciseId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserWritingAttempts_UserId_WritingExerciseSentenceId_CreatedAtUtc",
                table: "UserWritingAttempts",
                columns: new[] { "UserID", "WritingExerciseSentenceId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserWritingAttempts_WritingExerciseId",
                table: "UserWritingAttempts",
                column: "WritingExerciseId");

            migrationBuilder.CreateIndex(
                name: "IX_UserWritingAttempts_WritingExerciseSentenceId",
                table: "UserWritingAttempts",
                column: "WritingExerciseSentenceId");

            migrationBuilder.CreateIndex(
                name: "IX_UserWritingExerciseProgresses_UserId_WritingExerciseId",
                table: "UserWritingExerciseProgresses",
                columns: new[] { "UserID", "WritingExerciseID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserWritingExerciseProgresses_WritingExerciseID",
                table: "UserWritingExerciseProgresses",
                column: "WritingExerciseID");

            migrationBuilder.CreateIndex(
                name: "IX_UserWritingSentenceProgresses_SentenceID",
                table: "UserWritingSentenceProgresses",
                column: "SentenceID");

            migrationBuilder.CreateIndex(
                name: "IX_UserWritingSentenceProgresses_UserId_SentenceId",
                table: "UserWritingSentenceProgresses",
                columns: new[] { "UserID", "SentenceID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserWritingSentenceProgresses_WritingExerciseID",
                table: "UserWritingSentenceProgresses",
                column: "WritingExerciseID");

            migrationBuilder.CreateIndex(
                name: "IX_WritingExercises_UserId_IsPublished_CreatedAt",
                table: "WritingExercises",
                columns: new[] { "UserID", "IsPublished", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WritingExercises_UserId_IsPublished_Level_ContentType_Topic",
                table: "WritingExercises",
                columns: new[] { "UserID", "IsPublished", "Level", "ContentType", "Topic" });

            migrationBuilder.CreateIndex(
                name: "IX_WritingExerciseSentences_WritingExerciseId_SortOrder",
                table: "WritingExerciseSentences",
                columns: new[] { "WritingExerciseId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WritingGenerationLogs_UserId_RequestedAtUtc",
                table: "WritingGenerationLogs",
                columns: new[] { "UserID", "RequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WritingGenerationLogs_UserId_RequestType_RequestedAtUtc",
                table: "WritingGenerationLogs",
                columns: new[] { "UserID", "RequestType", "RequestedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiMessages");

            migrationBuilder.DropTable(
                name: "AiRequestLogs");

            migrationBuilder.DropTable(
                name: "ClassFolders");

            migrationBuilder.DropTable(
                name: "ClassMembers");

            migrationBuilder.DropTable(
                name: "ClassMessages");

            migrationBuilder.DropTable(
                name: "LearningProgress");

            migrationBuilder.DropTable(
                name: "ListeningQuizQuestions");

            migrationBuilder.DropTable(
                name: "ListeningTranscriptLines");

            migrationBuilder.DropTable(
                name: "ListeningVocabItems");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "PaymentAdminActions");

            migrationBuilder.DropTable(
                name: "PaymentEvents");

            migrationBuilder.DropTable(
                name: "ReadingOptions");

            migrationBuilder.DropTable(
                name: "SavedFolders");

            migrationBuilder.DropTable(
                name: "UserBadges");

            migrationBuilder.DropTable(
                name: "UserDailyActivities");

            migrationBuilder.DropTable(
                name: "UserGoals");

            migrationBuilder.DropTable(
                name: "UserListeningProgresses");

            migrationBuilder.DropTable(
                name: "UserReadingHistories");

            migrationBuilder.DropTable(
                name: "UserSpeakingProgresses");

            migrationBuilder.DropTable(
                name: "UserSpeakingVideoCompletions");

            migrationBuilder.DropTable(
                name: "UserWritingAttempts");

            migrationBuilder.DropTable(
                name: "UserWritingExerciseProgresses");

            migrationBuilder.DropTable(
                name: "UserWritingSentenceProgresses");

            migrationBuilder.DropTable(
                name: "WritingGenerationLogs");

            migrationBuilder.DropTable(
                name: "AiConversations");

            migrationBuilder.DropTable(
                name: "Classes");

            migrationBuilder.DropTable(
                name: "Cards");

            migrationBuilder.DropTable(
                name: "UserSubscriptions");

            migrationBuilder.DropTable(
                name: "ReadingQuestions");

            migrationBuilder.DropTable(
                name: "Badges");

            migrationBuilder.DropTable(
                name: "ListeningLessons");

            migrationBuilder.DropTable(
                name: "SpeakingSentences");

            migrationBuilder.DropTable(
                name: "WritingExerciseSentences");

            migrationBuilder.DropTable(
                name: "Sets");

            migrationBuilder.DropTable(
                name: "PaymentOrders");

            migrationBuilder.DropTable(
                name: "ReadingPassages");

            migrationBuilder.DropTable(
                name: "SpeakingVideos");

            migrationBuilder.DropTable(
                name: "WritingExercises");

            migrationBuilder.DropTable(
                name: "Folders");

            migrationBuilder.DropTable(
                name: "PremiumPlans");

            migrationBuilder.DropTable(
                name: "SpeakingPlaylists");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
