using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinuxGameCompat.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceReferenceImportState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SourceReferenceImportStates",
                columns: table => new
                {
                    SourceReferenceId = table.Column<int>(type: "integer", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ContractVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    LastAttemptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSucceededAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ETag = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceReferenceImportStates", x => x.SourceReferenceId);
                    table.ForeignKey(
                        name: "FK_SourceReferenceImportStates_SourceReferences_SourceReferenc~",
                        column: x => x.SourceReferenceId,
                        principalTable: "SourceReferences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SourceReferenceImportStates");
        }
    }
}
