using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MindMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompleteSocialPlatformSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdminFreezeReason",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AdminFrozenUntilUtc",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FrozenByAdminId",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFrozenByAdmin",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "User");

            migrationBuilder.AddColumn<DateTime>(
                name: "SelfDeactivatedAtUtc",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SessionVersion",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "AllowCallsFromNonMatches",
                table: "user_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AllowMessageRequests",
                table: "user_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Nickname",
                table: "user_profiles",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedNickname",
                table: "user_profiles",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE user_profiles
                SET "Nickname" = 'user' || SUBSTRING(REPLACE("UserId"::text, '-', '') FROM 1 FOR 26),
                    "NormalizedNickname" = UPPER('user' || SUBSTRING(REPLACE("UserId"::text, '-', '') FROM 1 FOR 26))
                WHERE "Nickname" = '' OR "NormalizedNickname" = '';
                """);

            migrationBuilder.AddColumn<bool>(
                name: "PremiumPriorityOptIn",
                table: "user_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowLastSeen",
                table: "user_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowOnlineStatus",
                table: "user_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowPremiumBadge",
                table: "user_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowReadReceipts",
                table: "user_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "refresh_tokens",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ConversationId",
                table: "matches",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserPairKey",
                table: "matches",
                type: "character varying(66)",
                maxLength: 66,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE matches
                SET "UserPairKey" = CASE
                    WHEN REPLACE("RequesterId"::text, '-', '') COLLATE "C" < REPLACE("ReceiverId"::text, '-', '') COLLATE "C"
                    THEN REPLACE("RequesterId"::text, '-', '') || ':' || REPLACE("ReceiverId"::text, '-', '')
                    ELSE REPLACE("ReceiverId"::text, '-', '') || ':' || REPLACE("RequesterId"::text, '-', '')
                END;

                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM matches
                        WHERE "Status" IN ('Pending', 'Accepted')
                        GROUP BY "UserPairKey"
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot enforce unique active match pairs: duplicate pending or accepted matches exist.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "matches",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "conversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    User1Id = table.Column<Guid>(type: "uuid", nullable: false),
                    User2Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LastMessageAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    MessageRetentionDays = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    User1HasHiddenConversation = table.Column<bool>(type: "boolean", nullable: false),
                    User2HasHiddenConversation = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_conversations_users_User1Id",
                        column: x => x.User1Id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_conversations_users_User2Id",
                        column: x => x.User2Id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "discovery_decisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ViewerId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RejectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_discovery_decisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_discovery_decisions_users_TargetUserId",
                        column: x => x.TargetUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_discovery_decisions_users_ViewerId",
                        column: x => x.ViewerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Body = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReadAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notifications_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "password_reset_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_password_reset_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_password_reset_tokens_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "premium_entitlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ExternalReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_premium_entitlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_premium_entitlements_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "realtime_outbox_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    DestinationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_realtime_outbox_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_blocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BlockerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlockedUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_blocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_blocks_users_BlockedUserId",
                        column: x => x.BlockedUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_blocks_users_BlockerId",
                        column: x => x.BlockerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_music_preferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Genre = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    NormalizedGenre = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_music_preferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_music_preferences_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_reports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReporterId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportedUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AssignedModeratorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Resolution = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_reports_users_AssignedModeratorId",
                        column: x => x.AssignedModeratorId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_reports_users_ReportedUserId",
                        column: x => x.ReportedUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_reports_users_ReporterId",
                        column: x => x.ReporterId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "calls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CallerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiverId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AnsweredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_calls_conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_calls_users_CallerId",
                        column: x => x.CallerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_calls_users_ReceiverId",
                        column: x => x.ReceiverId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "media_assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LengthBytes = table.Column<long>(type: "bigint", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: true),
                    Height = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: true),
                    BindingVersion = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_media_assets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_media_assets_conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_media_assets_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "message_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiverId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    RespondedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_message_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_message_requests_conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_message_requests_users_ReceiverId",
                        column: x => x.ReceiverId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_message_requests_users_SenderId",
                        column: x => x.SenderId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "moderation_audits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReportId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_moderation_audits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_moderation_audits_user_reports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "user_reports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_moderation_audits_users_ActorId",
                        column: x => x.ActorId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_moderation_audits_users_TargetUserId",
                        column: x => x.TargetUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MediaUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    MediaAssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReadAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeletedForEveryone = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_messages_conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_messages_media_assets_MediaAssetId",
                        column: x => x.MediaAssetId,
                        principalTable: "media_assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_messages_users_SenderId",
                        column: x => x.SenderId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "profile_photos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profile_photos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_profile_photos_media_assets_MediaAssetId",
                        column: x => x.MediaAssetId,
                        principalTable: "media_assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_profile_photos_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MediaUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    MediaAssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    Caption = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stories_media_assets_MediaAssetId",
                        column: x => x.MediaAssetId,
                        principalTable: "media_assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stories_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "message_deletions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_message_deletions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_message_deletions_messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_message_deletions_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reported_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    MediaAssetIdSnapshot = table.Column<Guid>(type: "uuid", nullable: true),
                    SenderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentSnapshot = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    MediaReferenceSnapshot = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    MessageType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MessageCreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EvidenceExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reported_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_reported_messages_messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_reported_messages_user_reports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "user_reports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_reported_messages_users_SenderId",
                        column: x => x.SenderId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "story_views",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ViewerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ViewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_story_views", x => x.Id);
                    table.ForeignKey(
                        name: "FK_story_views_stories_StoryId",
                        column: x => x.StoryId,
                        principalTable: "stories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_story_views_users_ViewerId",
                        column: x => x.ViewerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_IsFrozenByAdmin_AdminFrozenUntilUtc",
                table: "users",
                columns: new[] { "IsFrozenByAdmin", "AdminFrozenUntilUtc" },
                filter: "\"IsFrozenByAdmin\" = TRUE AND \"AdminFrozenUntilUtc\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_user_profiles_NormalizedNickname",
                table: "user_profiles",
                column: "NormalizedNickname",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_matches_ConversationId",
                table: "matches",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_matches_UserPairKey",
                table: "matches",
                column: "UserPairKey",
                unique: true,
                filter: "\"Status\" IN ('Pending', 'Accepted')");

            migrationBuilder.CreateIndex(
                name: "IX_calls_CallerId",
                table: "calls",
                column: "CallerId");

            migrationBuilder.CreateIndex(
                name: "IX_calls_ConversationId_StartedAtUtc",
                table: "calls",
                columns: new[] { "ConversationId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_calls_ReceiverId",
                table: "calls",
                column: "ReceiverId");

            migrationBuilder.CreateIndex(
                name: "IX_conversations_User1Id_IsActive_LastMessageAtUtc",
                table: "conversations",
                columns: new[] { "User1Id", "IsActive", "LastMessageAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_conversations_User1Id_User2Id",
                table: "conversations",
                columns: new[] { "User1Id", "User2Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_conversations_User2Id_IsActive_LastMessageAtUtc",
                table: "conversations",
                columns: new[] { "User2Id", "IsActive", "LastMessageAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_discovery_decisions_TargetUserId",
                table: "discovery_decisions",
                column: "TargetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_discovery_decisions_ViewerId_RejectedAtUtc_SeenAtUtc",
                table: "discovery_decisions",
                columns: new[] { "ViewerId", "RejectedAtUtc", "SeenAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_discovery_decisions_ViewerId_TargetUserId",
                table: "discovery_decisions",
                columns: new[] { "ViewerId", "TargetUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_media_assets_ConversationId",
                table: "media_assets",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_media_assets_CreatedAtUtc",
                table: "media_assets",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_media_assets_IsDeleted_CreatedAtUtc",
                table: "media_assets",
                columns: new[] { "IsDeleted", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_media_assets_OwnerId_Category_CreatedAtUtc",
                table: "media_assets",
                columns: new[] { "OwnerId", "Category", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_message_deletions_MessageId_UserId",
                table: "message_deletions",
                columns: new[] { "MessageId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_message_deletions_UserId_CreatedAtUtc",
                table: "message_deletions",
                columns: new[] { "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_message_requests_ConversationId",
                table: "message_requests",
                column: "ConversationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_message_requests_ReceiverId_Status_CreatedAtUtc",
                table: "message_requests",
                columns: new[] { "ReceiverId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_message_requests_SenderId",
                table: "message_requests",
                column: "SenderId");

            migrationBuilder.CreateIndex(
                name: "IX_messages_ConversationId_CreatedAtUtc",
                table: "messages",
                columns: new[] { "ConversationId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_messages_ExpiresAtUtc",
                table: "messages",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_messages_MediaAssetId",
                table: "messages",
                column: "MediaAssetId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_messages_SenderId_CreatedAtUtc",
                table: "messages",
                columns: new[] { "SenderId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_moderation_audits_ActorId",
                table: "moderation_audits",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_moderation_audits_ReportId_CreatedAtUtc",
                table: "moderation_audits",
                columns: new[] { "ReportId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_moderation_audits_TargetUserId_CreatedAtUtc",
                table: "moderation_audits",
                columns: new[] { "TargetUserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_UserId_CreatedAtUtc",
                table: "notifications",
                columns: new[] { "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_UserId_ReadAtUtc_CreatedAtUtc",
                table: "notifications",
                columns: new[] { "UserId", "ReadAtUtc", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_tokens_TokenHash",
                table: "password_reset_tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_tokens_UserId_ExpiresAtUtc_UsedAtUtc",
                table: "password_reset_tokens",
                columns: new[] { "UserId", "ExpiresAtUtc", "UsedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_premium_entitlements_Source_ExternalReference",
                table: "premium_entitlements",
                columns: new[] { "Source", "ExternalReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_premium_entitlements_UserId_StartsAtUtc_ExpiresAtUtc_IsRevo~",
                table: "premium_entitlements",
                columns: new[] { "UserId", "StartsAtUtc", "ExpiresAtUtc", "IsRevoked" });

            migrationBuilder.CreateIndex(
                name: "IX_profile_photos_MediaAssetId",
                table: "profile_photos",
                column: "MediaAssetId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_profile_photos_UserId",
                table: "profile_photos",
                column: "UserId",
                unique: true,
                filter: "\"IsPrimary\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_profile_photos_UserId_IsPrimary",
                table: "profile_photos",
                columns: new[] { "UserId", "IsPrimary" });

            migrationBuilder.CreateIndex(
                name: "IX_profile_photos_UserId_SortOrder",
                table: "profile_photos",
                columns: new[] { "UserId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_realtime_outbox_messages_DestinationType_DestinationId_Sequ~",
                table: "realtime_outbox_messages",
                columns: new[] { "DestinationType", "DestinationId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_realtime_outbox_messages_ExpiresAtUtc",
                table: "realtime_outbox_messages",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_realtime_outbox_messages_ProcessedAtUtc_NextAttemptAtUtc_Se~",
                table: "realtime_outbox_messages",
                columns: new[] { "ProcessedAtUtc", "NextAttemptAtUtc", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_reported_messages_EvidenceExpiresAtUtc",
                table: "reported_messages",
                column: "EvidenceExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_reported_messages_MediaAssetIdSnapshot",
                table: "reported_messages",
                column: "MediaAssetIdSnapshot");

            migrationBuilder.CreateIndex(
                name: "IX_reported_messages_MessageId",
                table: "reported_messages",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_reported_messages_ReportId_MessageCreatedAtUtc",
                table: "reported_messages",
                columns: new[] { "ReportId", "MessageCreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_reported_messages_SenderId",
                table: "reported_messages",
                column: "SenderId");

            migrationBuilder.CreateIndex(
                name: "IX_stories_IsDeleted_ExpiresAtUtc",
                table: "stories",
                columns: new[] { "IsDeleted", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_stories_MediaAssetId",
                table: "stories",
                column: "MediaAssetId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stories_UserId_ExpiresAtUtc",
                table: "stories",
                columns: new[] { "UserId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_story_views_StoryId_ViewerId",
                table: "story_views",
                columns: new[] { "StoryId", "ViewerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_story_views_ViewerId",
                table: "story_views",
                column: "ViewerId");

            migrationBuilder.CreateIndex(
                name: "IX_user_blocks_BlockedUserId_BlockerId",
                table: "user_blocks",
                columns: new[] { "BlockedUserId", "BlockerId" });

            migrationBuilder.CreateIndex(
                name: "IX_user_blocks_BlockerId_BlockedUserId",
                table: "user_blocks",
                columns: new[] { "BlockerId", "BlockedUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_music_preferences_NormalizedGenre",
                table: "user_music_preferences",
                column: "NormalizedGenre");

            migrationBuilder.CreateIndex(
                name: "IX_user_music_preferences_UserId_NormalizedGenre",
                table: "user_music_preferences",
                columns: new[] { "UserId", "NormalizedGenre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_reports_AssignedModeratorId",
                table: "user_reports",
                column: "AssignedModeratorId");

            migrationBuilder.CreateIndex(
                name: "IX_user_reports_ReportedUserId_CreatedAtUtc",
                table: "user_reports",
                columns: new[] { "ReportedUserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_user_reports_ReporterId_CreatedAtUtc",
                table: "user_reports",
                columns: new[] { "ReporterId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_user_reports_Status_CreatedAtUtc",
                table: "user_reports",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_matches_conversations_ConversationId",
                table: "matches",
                column: "ConversationId",
                principalTable: "conversations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_matches_conversations_ConversationId",
                table: "matches");

            migrationBuilder.DropTable(
                name: "calls");

            migrationBuilder.DropTable(
                name: "discovery_decisions");

            migrationBuilder.DropTable(
                name: "message_deletions");

            migrationBuilder.DropTable(
                name: "message_requests");

            migrationBuilder.DropTable(
                name: "moderation_audits");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "password_reset_tokens");

            migrationBuilder.DropTable(
                name: "premium_entitlements");

            migrationBuilder.DropTable(
                name: "profile_photos");

            migrationBuilder.DropTable(
                name: "realtime_outbox_messages");

            migrationBuilder.DropTable(
                name: "reported_messages");

            migrationBuilder.DropTable(
                name: "story_views");

            migrationBuilder.DropTable(
                name: "user_blocks");

            migrationBuilder.DropTable(
                name: "user_music_preferences");

            migrationBuilder.DropTable(
                name: "messages");

            migrationBuilder.DropTable(
                name: "user_reports");

            migrationBuilder.DropTable(
                name: "stories");

            migrationBuilder.DropTable(
                name: "media_assets");

            migrationBuilder.DropTable(
                name: "conversations");

            migrationBuilder.DropIndex(
                name: "IX_users_IsFrozenByAdmin_AdminFrozenUntilUtc",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_user_profiles_NormalizedNickname",
                table: "user_profiles");

            migrationBuilder.DropIndex(
                name: "IX_matches_ConversationId",
                table: "matches");

            migrationBuilder.DropIndex(
                name: "IX_matches_UserPairKey",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "AdminFreezeReason",
                table: "users");

            migrationBuilder.DropColumn(
                name: "AdminFrozenUntilUtc",
                table: "users");

            migrationBuilder.DropColumn(
                name: "FrozenByAdminId",
                table: "users");

            migrationBuilder.DropColumn(
                name: "IsFrozenByAdmin",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "users");

            migrationBuilder.DropColumn(
                name: "SelfDeactivatedAtUtc",
                table: "users");

            migrationBuilder.DropColumn(
                name: "SessionVersion",
                table: "users");

            migrationBuilder.DropColumn(
                name: "AllowCallsFromNonMatches",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "AllowMessageRequests",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "Nickname",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "NormalizedNickname",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "PremiumPriorityOptIn",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "ShowLastSeen",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "ShowOnlineStatus",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "ShowPremiumBadge",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "ShowReadReceipts",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "ConversationId",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "UserPairKey",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "matches");
        }
    }
}
