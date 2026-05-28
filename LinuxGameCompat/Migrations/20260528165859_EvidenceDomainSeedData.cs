using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LinuxGameCompat.Migrations
{
    /// <inheritdoc />
    public partial class EvidenceDomainSeedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Games",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SteamAppId = table.Column<int>(type: "integer", nullable: true),
                    Slug = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    CompatibilityStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IsHidden = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Games", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SourceSystems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    BaseUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceSystems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GameCompatibilitySummaries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GameId = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SummaryStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SummaryText = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Model = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    EvidenceVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    EvidenceHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsStale = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameCompatibilitySummaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameCompatibilitySummaries_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SourceReferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GameId = table.Column<int>(type: "integer", nullable: false),
                    SourceSystemId = table.Column<int>(type: "integer", nullable: false),
                    SourceGameId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceReferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SourceReferences_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SourceReferences_SourceSystems_SourceSystemId",
                        column: x => x.SourceSystemId,
                        principalTable: "SourceSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EvidenceClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GameId = table.Column<int>(type: "integer", nullable: false),
                    SourceSystemId = table.Column<int>(type: "integer", nullable: false),
                    SourceReferenceId = table.Column<int>(type: "integer", nullable: false),
                    ClaimType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ClaimValue = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ClaimText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ObservedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvidenceClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvidenceClaims_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EvidenceClaims_SourceReferences_SourceReferenceId",
                        column: x => x.SourceReferenceId,
                        principalTable: "SourceReferences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EvidenceClaims_SourceSystems_SourceSystemId",
                        column: x => x.SourceSystemId,
                        principalTable: "SourceSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Games",
                columns: new[] { "Id", "CompatibilityStatus", "CreatedAt", "IsHidden", "Slug", "SteamAppId", "Title", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, "Playable", new DateTimeOffset(new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), false, "baldurs-gate-3", 1086940, "Baldur's Gate 3", new DateTimeOffset(new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 2, "PlayableWithCaveats", new DateTimeOffset(new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), false, "helldivers-2", 553850, "Helldivers 2", new DateTimeOffset(new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 3, "Unsupported", new DateTimeOffset(new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), false, "destiny-2", 1085660, "Destiny 2", new DateTimeOffset(new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 4, "Unknown", new DateTimeOffset(new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), false, "unnamed-prototype", null, "Unnamed Prototype", new DateTimeOffset(new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 5, "Unknown", new DateTimeOffset(new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "suppressed-test-record", 999001, "Suppressed Test Record", new DateTimeOffset(new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });

            migrationBuilder.InsertData(
                table: "SourceSystems",
                columns: new[] { "Id", "BaseUrl", "Name", "Type" },
                values: new object[,]
                {
                    { 1, "https://www.protondb.com", "ProtonDB", "ProtonDb" },
                    { 2, "https://areweanticheatyet.com", "Are We Anti-Cheat Yet?", "AreWeAntiCheatYet" }
                });

            migrationBuilder.InsertData(
                table: "GameCompatibilitySummaries",
                columns: new[] { "Id", "ErrorCode", "ErrorMessage", "EvidenceHash", "EvidenceVersion", "GameId", "GeneratedAt", "IsStale", "Model", "Provider", "State", "SummaryStatus", "SummaryText" },
                values: new object[,]
                {
                    { 1, null, null, "seed-baldurs-gate-3-v1", "seed-v1", 1, new DateTimeOffset(new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), false, "manual-baseline", "placeholder", "Current", "Playable", "Playable through Proton based on stored source-backed evidence." },
                    { 2, null, null, "seed-helldivers-2-v1", "seed-v1", 2, null, true, null, null, "NotGenerated", "PlayableWithCaveats", null }
                });

            migrationBuilder.InsertData(
                table: "SourceReferences",
                columns: new[] { "Id", "CreatedAt", "GameId", "MetadataJson", "SourceGameId", "SourceSystemId", "Url" },
                values: new object[,]
                {
                    { 1, new DateTimeOffset(new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1, "{\"source\":\"protondb\",\"kind\":\"steam-app\"}", "1086940", 1, "https://www.protondb.com/app/1086940" },
                    { 2, new DateTimeOffset(new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, "{\"source\":\"protondb\",\"kind\":\"steam-app\"}", "553850", 1, "https://www.protondb.com/app/553850" },
                    { 3, new DateTimeOffset(new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, "{\"source\":\"areweanticheatyet\",\"kind\":\"game-page\"}", "helldivers-2", 2, "https://areweanticheatyet.com/game/helldivers-2" },
                    { 4, new DateTimeOffset(new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 3, "{\"source\":\"areweanticheatyet\",\"kind\":\"game-page\"}", "destiny-2", 2, "https://areweanticheatyet.com/game/destiny-2" }
                });

            migrationBuilder.InsertData(
                table: "EvidenceClaims",
                columns: new[] { "Id", "ClaimText", "ClaimType", "ClaimValue", "GameId", "ObservedAt", "SourceReferenceId", "SourceSystemId" },
                values: new object[,]
                {
                    { 1, "Community reports indicate the game is playable through Proton.", "Status", "playable", 1, new DateTimeOffset(new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1, 1 },
                    { 2, "Anti-cheat support is a compatibility consideration for multiplayer sessions.", "Caveat", "anti-cheat", 2, new DateTimeOffset(new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 3, 2 },
                    { 3, "Anti-cheat policy blocks Linux/Proton play.", "Status", "unsupported", 3, new DateTimeOffset(new DateTime(2026, 5, 28, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 4, 2 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceClaims_GameId_ClaimType",
                table: "EvidenceClaims",
                columns: new[] { "GameId", "ClaimType" });

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceClaims_SourceReferenceId",
                table: "EvidenceClaims",
                column: "SourceReferenceId");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceClaims_SourceSystemId",
                table: "EvidenceClaims",
                column: "SourceSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_GameCompatibilitySummaries_GameId",
                table: "GameCompatibilitySummaries",
                column: "GameId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Games_IsHidden_Title",
                table: "Games",
                columns: new[] { "IsHidden", "Title" });

            migrationBuilder.CreateIndex(
                name: "IX_Games_Slug",
                table: "Games",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Games_SteamAppId",
                table: "Games",
                column: "SteamAppId",
                unique: true,
                filter: "\"SteamAppId\" IS NOT NULL");

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
                name: "IX_SourceReferences_Url",
                table: "SourceReferences",
                column: "Url");

            migrationBuilder.CreateIndex(
                name: "IX_SourceSystems_Type",
                table: "SourceSystems",
                column: "Type",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EvidenceClaims");

            migrationBuilder.DropTable(
                name: "GameCompatibilitySummaries");

            migrationBuilder.DropTable(
                name: "SourceReferences");

            migrationBuilder.DropTable(
                name: "Games");

            migrationBuilder.DropTable(
                name: "SourceSystems");
        }
    }
}
