using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TCTVocabulary.Models;

#nullable disable

namespace TCTVocabulary.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(DbflashcardContext))]
    [Migration("20260311102000_NormalizeLockExpiryColumnType")]
    public partial class NormalizeLockExpiryColumnType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Users', 'LockExpiry') IS NULL
BEGIN
    ALTER TABLE [dbo].[Users] ADD [LockExpiry] datetime2 NULL;
END
ELSE
BEGIN
    DECLARE @lockExpiryType nvarchar(128);

    SELECT @lockExpiryType = DATA_TYPE
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'dbo'
      AND TABLE_NAME = 'Users'
      AND COLUMN_NAME = 'LockExpiry';

    IF @lockExpiryType IN ('varchar', 'nvarchar', 'char', 'nchar')
    BEGIN
        -- 1) Null-out malformed values that cannot be converted
        UPDATE [dbo].[Users]
        SET [LockExpiry] = NULL
        WHERE [LockExpiry] IS NOT NULL
          AND TRY_CONVERT(datetime2, CONVERT(nvarchar(255), [LockExpiry])) IS NULL;

        -- 2) Normalize remaining valid values to ISO-8601 text before ALTER COLUMN
        UPDATE [dbo].[Users]
        SET [LockExpiry] = CONVERT(nvarchar(33), TRY_CONVERT(datetime2, CONVERT(nvarchar(255), [LockExpiry])), 126)
        WHERE [LockExpiry] IS NOT NULL;

        ALTER TABLE [dbo].[Users] ALTER COLUMN [LockExpiry] datetime2 NULL;
    END
    ELSE IF @lockExpiryType = 'datetime'
    BEGIN
        ALTER TABLE [dbo].[Users] ALTER COLUMN [LockExpiry] datetime2 NULL;
    END
    ELSE IF @lockExpiryType <> 'datetime2'
    BEGIN
        ALTER TABLE [dbo].[Users] ALTER COLUMN [LockExpiry] datetime2 NULL;
    END
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Users', 'LockExpiry') IS NOT NULL
BEGIN
    DECLARE @lockExpiryType nvarchar(128);

    SELECT @lockExpiryType = DATA_TYPE
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'dbo'
      AND TABLE_NAME = 'Users'
      AND COLUMN_NAME = 'LockExpiry';

    IF @lockExpiryType = 'datetime2'
    BEGIN
        ALTER TABLE [dbo].[Users] ALTER COLUMN [LockExpiry] datetime NULL;
    END
END
");
        }
    }
}
