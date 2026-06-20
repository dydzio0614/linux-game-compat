using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinuxGameCompat.Migrations
{
    /// <inheritdoc />
    public partial class AddSummaryAttemptMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InputTokenCount",
                table: "GameCompatibilitySummaries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastAttemptedAt",
                table: "GameCompatibilitySummaries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OutputTokenCount",
                table: "GameCompatibilitySummaries",
                type: "integer",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "GameCompatibilitySummaries",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "InputTokenCount", "LastAttemptedAt", "OutputTokenCount" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "GameCompatibilitySummaries",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "InputTokenCount", "LastAttemptedAt", "OutputTokenCount" },
                values: new object[] { null, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
			throw new NotSupportedException("This additive migration is forward-only.");
        }
    }
}
