using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinuxGameCompat.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeEvidenceClaimCitation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EvidenceClaims_Games_GameId",
                table: "EvidenceClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_EvidenceClaims_SourceSystems_SourceSystemId",
                table: "EvidenceClaims");

            migrationBuilder.DropIndex(
                name: "IX_SourceReferences_GameId_SourceSystemId_SourceGameId",
                table: "SourceReferences");

            migrationBuilder.DropIndex(
                name: "IX_SourceReferences_SourceSystemId",
                table: "SourceReferences");

            migrationBuilder.DropIndex(
                name: "IX_EvidenceClaims_GameId_ClaimType",
                table: "EvidenceClaims");

            migrationBuilder.DropIndex(
                name: "IX_EvidenceClaims_SourceSystemId",
                table: "EvidenceClaims");

            migrationBuilder.DropColumn(
                name: "GameId",
                table: "EvidenceClaims");

            migrationBuilder.DropColumn(
                name: "SourceSystemId",
                table: "EvidenceClaims");

            migrationBuilder.CreateIndex(
                name: "IX_SourceReferences_GameId",
                table: "SourceReferences",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_SourceReferences_SourceSystemId_SourceGameId",
                table: "SourceReferences",
                columns: new[] { "SourceSystemId", "SourceGameId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SourceReferences_GameId",
                table: "SourceReferences");

            migrationBuilder.DropIndex(
                name: "IX_SourceReferences_SourceSystemId_SourceGameId",
                table: "SourceReferences");

            migrationBuilder.AddColumn<int>(
                name: "GameId",
                table: "EvidenceClaims",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SourceSystemId",
                table: "EvidenceClaims",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "EvidenceClaims",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "GameId", "SourceSystemId" },
                values: new object[] { 1, 1 });

            migrationBuilder.UpdateData(
                table: "EvidenceClaims",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "GameId", "SourceSystemId" },
                values: new object[] { 2, 2 });

            migrationBuilder.UpdateData(
                table: "EvidenceClaims",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "GameId", "SourceSystemId" },
                values: new object[] { 3, 2 });

            migrationBuilder.CreateIndex(
                name: "IX_SourceReferences_GameId_SourceSystemId_SourceGameId",
                table: "SourceReferences",
                columns: new[] { "GameId", "SourceSystemId", "SourceGameId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SourceReferences_SourceSystemId",
                table: "SourceReferences",
                column: "SourceSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceClaims_GameId_ClaimType",
                table: "EvidenceClaims",
                columns: new[] { "GameId", "ClaimType" });

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceClaims_SourceSystemId",
                table: "EvidenceClaims",
                column: "SourceSystemId");

            migrationBuilder.AddForeignKey(
                name: "FK_EvidenceClaims_Games_GameId",
                table: "EvidenceClaims",
                column: "GameId",
                principalTable: "Games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EvidenceClaims_SourceSystems_SourceSystemId",
                table: "EvidenceClaims",
                column: "SourceSystemId",
                principalTable: "SourceSystems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
