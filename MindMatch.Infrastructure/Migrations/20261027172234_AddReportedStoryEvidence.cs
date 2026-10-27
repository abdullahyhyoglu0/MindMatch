using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MindMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReportedStoryEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reported_stories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    MediaAssetIdSnapshot = table.Column<Guid>(type: "uuid", nullable: true),
                    MediaReferenceSnapshot = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    MediaType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CaptionSnapshot = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    StoryCreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EvidenceExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reported_stories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_reported_stories_user_reports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "user_reports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reported_stories_EvidenceExpiresAtUtc",
                table: "reported_stories",
                column: "EvidenceExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_reported_stories_MediaAssetIdSnapshot",
                table: "reported_stories",
                column: "MediaAssetIdSnapshot");

            migrationBuilder.CreateIndex(
                name: "IX_reported_stories_ReportId_StoryCreatedAtUtc",
                table: "reported_stories",
                columns: new[] { "ReportId", "StoryCreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reported_stories");
        }
    }
}
