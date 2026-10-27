using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MindMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompleteFollowAndPrivacySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShowFollowers",
                table: "user_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowFollowing",
                table: "user_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "user_follows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FollowerId = table.Column<Guid>(type: "uuid", nullable: false),
                    FollowedId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    RespondedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_follows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_follows_users_FollowedId",
                        column: x => x.FollowedId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_follows_users_FollowerId",
                        column: x => x.FollowerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_follows_FollowedId_Status_CreatedAtUtc",
                table: "user_follows",
                columns: new[] { "FollowedId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_user_follows_FollowerId_FollowedId",
                table: "user_follows",
                columns: new[] { "FollowerId", "FollowedId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_follows_FollowerId_Status_CreatedAtUtc",
                table: "user_follows",
                columns: new[] { "FollowerId", "Status", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_follows");

            migrationBuilder.DropColumn(
                name: "ShowFollowers",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "ShowFollowing",
                table: "user_profiles");
        }
    }
}
