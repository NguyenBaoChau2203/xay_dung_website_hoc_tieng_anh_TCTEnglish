using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCTEnglish.Migrations
{
    /// <inheritdoc />
    public partial class AddAiQuotaTurnStage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "ActiveStage",
                table: "AiQuotaReservationLedgers",
                type: "tinyint unsigned",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KnownEmbeddingTokens",
                table: "AiQuotaReservationLedgers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KnownGenerationTokens",
                table: "AiQuotaReservationLedgers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LeaseExpiresAtUtc",
                table: "AiQuotaReservationLedgers",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "TerminalIntent",
                table: "AiQuotaReservationLedgers",
                type: "tinyint unsigned",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UnknownEmbeddingTokens",
                table: "AiQuotaReservationLedgers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UnknownGenerationTokens",
                table: "AiQuotaReservationLedgers",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActiveStage",
                table: "AiQuotaReservationLedgers");

            migrationBuilder.DropColumn(
                name: "KnownEmbeddingTokens",
                table: "AiQuotaReservationLedgers");

            migrationBuilder.DropColumn(
                name: "KnownGenerationTokens",
                table: "AiQuotaReservationLedgers");

            migrationBuilder.DropColumn(
                name: "LeaseExpiresAtUtc",
                table: "AiQuotaReservationLedgers");

            migrationBuilder.DropColumn(
                name: "TerminalIntent",
                table: "AiQuotaReservationLedgers");

            migrationBuilder.DropColumn(
                name: "UnknownEmbeddingTokens",
                table: "AiQuotaReservationLedgers");

            migrationBuilder.DropColumn(
                name: "UnknownGenerationTokens",
                table: "AiQuotaReservationLedgers");
        }
    }
}
