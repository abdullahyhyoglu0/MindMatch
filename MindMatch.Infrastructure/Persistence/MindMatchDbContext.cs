

using Microsoft.EntityFrameworkCore;
using MindMatch.Application.Interfaces;
using MindMatch.Domain.Entities;

namespace MindMatch.Infrastructure.Persistence;

public class MindMatchDbContext : DbContext, IUnitOfWork
{
    public MindMatchDbContext(
        DbContextOptions<MindMatchDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<Interest> Interests => Set<Interest>();
    public DbSet<UserInterest> UserInterests => Set<UserInterest>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MoodCheckIn> MoodCheckIns => Set<MoodCheckIn>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<MessageDeletion> MessageDeletions => Set<MessageDeletion>();
    public DbSet<UserBlock> UserBlocks => Set<UserBlock>();
    public DbSet<UserReport> UserReports => Set<UserReport>();
    public DbSet<MessageRequest> MessageRequests => Set<MessageRequest>();
    public DbSet<ReportedMessage> ReportedMessages => Set<ReportedMessage>();
    public DbSet<ModerationAudit> ModerationAudits => Set<ModerationAudit>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<ProfilePhoto> ProfilePhotos => Set<ProfilePhoto>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<PremiumEntitlement> PremiumEntitlements => Set<PremiumEntitlement>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Call> Calls => Set<Call>();
    public DbSet<Story> Stories => Set<Story>();
    public DbSet<StoryView> StoryViews => Set<StoryView>();
    public DbSet<DiscoveryDecision> DiscoveryDecisions => Set<DiscoveryDecision>();
    public DbSet<UserMusicPreference> UserMusicPreferences => Set<UserMusicPreference>();
    public DbSet<RealtimeOutboxMessage> RealtimeOutboxMessages => Set<RealtimeOutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);


        // User
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Email)
                .HasMaxLength(256)
                .IsRequired();

            entity.Property(x => x.PasswordHash)
                .IsRequired();

            entity.Property(x => x.Role)
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasDefaultValue(UserRole.User)
                .IsRequired();

            entity.HasIndex(x => x.Email)
                .IsUnique();
            entity.HasIndex(x => new { x.IsFrozenByAdmin, x.AdminFrozenUntilUtc })
                .HasFilter("\"IsFrozenByAdmin\" = TRUE AND \"AdminFrozenUntilUtc\" IS NOT NULL");
        });

        // User - UserProfile: bire bir ilişki
        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.ToTable("user_profiles");

            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.UserId)
                .IsUnique();

            entity.HasOne<User>()
                .WithOne()
                .HasForeignKey<UserProfile>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(x => x.DisplayName)
                .HasMaxLength(40)
                .IsRequired();

            entity.Property(x => x.Nickname).HasMaxLength(30).IsRequired();
            entity.Property(x => x.NormalizedNickname).HasMaxLength(30).IsRequired();
            entity.HasIndex(x => x.NormalizedNickname).IsUnique();

            entity.Property(x => x.Bio)
                .HasMaxLength(300);

            entity.Property(x => x.City)
                .HasMaxLength(100);
        });

        modelBuilder.Entity<DiscoveryDecision>(entity =>
        {
            entity.ToTable("discovery_decisions");
            entity.HasKey(x => x.Id);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.ViewerId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.TargetUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.ViewerId, x.TargetUserId }).IsUnique();
            entity.HasIndex(x => new { x.ViewerId, x.RejectedAtUtc, x.SeenAtUtc });
        });

        modelBuilder.Entity<UserMusicPreference>(entity =>
        {
            entity.ToTable("user_music_preferences");
            entity.HasKey(x => x.Id);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(x => x.Genre).HasMaxLength(60).IsRequired();
            entity.Property(x => x.NormalizedGenre).HasMaxLength(60).IsRequired();
            entity.HasIndex(x => new { x.UserId, x.NormalizedGenre }).IsUnique();
            entity.HasIndex(x => x.NormalizedGenre);
        });

        modelBuilder.Entity<RealtimeOutboxMessage>(entity =>
        {
            entity.ToTable("realtime_outbox_messages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DestinationType).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.EventName).HasMaxLength(80).IsRequired();
            entity.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.LastError).HasMaxLength(500);
            entity.Property(x => x.Sequence).UseIdentityAlwaysColumn();
            entity.HasIndex(x => new { x.DestinationType, x.DestinationId, x.Sequence });
            entity.HasIndex(x => new { x.ProcessedAtUtc, x.NextAttemptAtUtc, x.Sequence });
            entity.HasIndex(x => x.ExpiresAtUtc);
        });

        // Interest
        modelBuilder.Entity<Interest>(entity =>
        {
            entity.ToTable("interests");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.NormalizedName)
                .HasMaxLength(50)
                .IsRequired();

            entity.HasIndex(x => x.NormalizedName)
                .IsUnique();
        });

        // User - Interest bağlantısı
        modelBuilder.Entity<UserInterest>(entity =>
        {
            entity.ToTable("user_interests");

            entity.HasKey(x => x.Id);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<Interest>()
                .WithMany()
                .HasForeignKey(x => x.InterestId)
                .OnDelete(DeleteBehavior.Cascade);

            // Aynı ilgi alanı aynı kullanıcıya iki kez eklenemez.
            entity.HasIndex(x => new
            {
                x.UserId,
                x.InterestId
            })
            .IsUnique();
        });

        // Match
        modelBuilder.Entity<Match>(entity =>
        {
            entity.ToTable("matches");

            entity.HasKey(x => x.Id);
            entity.HasOne(x => x.Conversation)
    .WithMany(x => x.Matches)
    .HasForeignKey(x => x.ConversationId)
    .OnDelete(DeleteBehavior.SetNull);


            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.RequesterId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(x => x.UserPairKey).HasMaxLength(66).IsRequired();
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.HasIndex(x => x.UserPairKey).IsUnique()
                .HasFilter("\"Status\" IN ('Pending', 'Accepted')");

            entity.HasIndex(x => new
            {
                x.RequesterId,
                x.ReceiverId,
                x.Status
            });

            // Gelen istekleri ve kullanıcıya ait eşleşmeleri sorgulamak için.
            entity.HasIndex(x => new
            {
                x.ReceiverId,
                x.Status,
                x.CreatedAtUtc
            });
        });

        // MoodCheckIn
        modelBuilder.Entity<MoodCheckIn>(entity =>
        {
            entity.ToTable("mood_check_ins");

            entity.HasKey(x => x.Id);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(x => x.Mood)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(x => x.Energy)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(x => x.DesiredConversation)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            // Kullanıcının aktif ruh hâlini kontrol etmek için.
            entity.HasIndex(x => new
            {
                x.UserId,
                x.ExpiresAtUtc
            });

            // En son ruh hâli kaydını bulmak için.
            entity.HasIndex(x => new
            {
                x.UserId,
                x.CreatedAtUtc
            });
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("notifications");
            entity.HasKey(x => x.Id);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Body).HasMaxLength(500).IsRequired();
            entity.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.UserId, x.ReadAtUtc, x.CreatedAtUtc });
        });

        modelBuilder.Entity<PremiumEntitlement>(entity =>
        {
            entity.ToTable("premium_entitlements");
            entity.HasKey(x => x.Id);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.Source).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(x => x.ExternalReference).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => new { x.Source, x.ExternalReference }).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.StartsAtUtc, x.ExpiresAtUtc, x.IsRevoked });
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.ToTable("password_reset_tokens");
            entity.HasKey(x => x.Id);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.ExpiresAtUtc, x.UsedAtUtc });
        });

        // RefreshToken
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Version).IsConcurrencyToken();

            entity.Property(x => x.TokenHash)
                .HasMaxLength(128)
                .IsRequired();

            entity.HasIndex(x => x.TokenHash)
                .IsUnique();

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });






        // Conversation
        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.ToTable("conversations");

            entity.HasKey(x => x.Id);
            entity.Property(x => x.MessageRetentionDays).HasDefaultValue(1).IsRequired();

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.User1Id)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.User2Id)
                .OnDelete(DeleteBehavior.Restrict);

            // Kullanıcı çifti ters sırayla da yazılabileceği için
            // tek başına bu indeks çift sohbeti tamamen engellemez.
            entity.HasIndex(x => new { x.User1Id, x.User2Id }).IsUnique();

            entity.HasIndex(x => new
            {
                x.User1Id,
                x.IsActive,
                x.LastMessageAtUtc
            });

            entity.HasIndex(x => new
            {
                x.User2Id,
                x.IsActive,
                x.LastMessageAtUtc
            });
        });

        // Message
        modelBuilder.Entity<Message>(entity =>
        {
            entity.ToTable("messages");

            entity.HasKey(x => x.Id);

            entity.HasOne(x => x.Conversation)
                .WithMany(x => x.Messages)
                .HasForeignKey(x => x.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<MediaAsset>()
                .WithMany()
                .HasForeignKey(x => x.MediaAssetId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(x => x.Content)
                .HasMaxLength(5000);

            entity.Property(x => x.Type)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(x => x.MediaUrl)
                .HasMaxLength(2048);

            entity.HasIndex(x => x.ExpiresAtUtc);

            entity.HasIndex(x => new
            {
                x.ConversationId,
                x.CreatedAtUtc
            });

            entity.HasIndex(x => new
            {
                x.SenderId,
                x.CreatedAtUtc
            });
            entity.HasIndex(x => x.MediaAssetId).IsUnique();
        });

        modelBuilder.Entity<MessageDeletion>(entity =>
        {
            entity.ToTable("message_deletions");
            entity.HasKey(x => x.Id);
            entity.HasOne<Message>()
                .WithMany(x => x.Deletions)
                .HasForeignKey(x => x.MessageId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.MessageId, x.UserId }).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
        });

        modelBuilder.Entity<UserBlock>(entity =>
        {
            entity.ToTable("user_blocks");
            entity.HasKey(x => x.Id);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.BlockerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.BlockedUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.BlockerId, x.BlockedUserId }).IsUnique();
            entity.HasIndex(x => new { x.BlockedUserId, x.BlockerId });
        });

        modelBuilder.Entity<ProfilePhoto>(entity =>
        {
            entity.ToTable("profile_photos");
            entity.HasKey(x => x.Id);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<MediaAsset>().WithMany().HasForeignKey(x => x.MediaAssetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.UserId, x.SortOrder }).IsUnique();
            entity.HasIndex(x => x.MediaAssetId).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.IsPrimary });
            entity.HasIndex(x => x.UserId).IsUnique().HasFilter("\"IsPrimary\" = TRUE");
        });

        modelBuilder.Entity<MediaAsset>(entity =>
        {
            entity.ToTable("media_assets");
            entity.HasKey(x => x.Id);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Conversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.BindingVersion).IsConcurrencyToken();
            entity.Property(x => x.Category).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(x => x.StorageKey).HasMaxLength(500).IsRequired();
            entity.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => new { x.OwnerId, x.Category, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.IsDeleted, x.CreatedAtUtc });
            entity.HasIndex(x => x.CreatedAtUtc);
        });

        modelBuilder.Entity<MessageRequest>(entity =>
        {
            entity.ToTable("message_requests");
            entity.HasKey(x => x.Id);
            entity.HasOne<Conversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.ReceiverId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.HasIndex(x => x.ConversationId).IsUnique();
            entity.HasIndex(x => new { x.ReceiverId, x.Status, x.CreatedAtUtc });
        });

        modelBuilder.Entity<UserReport>(entity =>
        {
            entity.ToTable("user_reports");
            entity.HasKey(x => x.Id);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.ReporterId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.ReportedUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.AssignedModeratorId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.Reason).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.Resolution).HasMaxLength(1000);
            entity.HasIndex(x => new { x.Status, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.ReporterId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.ReportedUserId, x.CreatedAtUtc });
            entity.HasMany(x => x.Messages).WithOne().HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReportedMessage>(entity =>
        {
            entity.ToTable("reported_messages");
            entity.HasKey(x => x.Id);
            entity.HasOne<Message>().WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.ContentSnapshot).HasMaxLength(5000);
            entity.Property(x => x.MediaReferenceSnapshot).HasMaxLength(2048);
            entity.HasIndex(x => x.EvidenceExpiresAtUtc);
            entity.HasIndex(x => x.MediaAssetIdSnapshot);
            entity.Property(x => x.MessageType).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.HasIndex(x => new { x.ReportId, x.MessageCreatedAtUtc });
        });

        modelBuilder.Entity<ModerationAudit>(entity =>
        {
            entity.ToTable("moderation_audits");
            entity.HasKey(x => x.Id);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.TargetUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<UserReport>().WithMany().HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.Action).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
            entity.HasIndex(x => new { x.TargetUserId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.ReportId, x.CreatedAtUtc });
        });

        // Call
        modelBuilder.Entity<Call>(entity =>
        {
            entity.ToTable("calls");

            entity.HasKey(x => x.Id);
            entity.Property(x => x.Version).IsConcurrencyToken();

            entity.HasOne(x => x.Conversation)
                .WithMany(x => x.Calls)
                .HasForeignKey(x => x.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.CallerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(x => x.Type)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            entity.HasIndex(x => new
            {
                x.ConversationId,
                x.StartedAtUtc
            });
        });

        // Story
        modelBuilder.Entity<Story>(entity =>
        {
            entity.ToTable("stories");

            entity.HasKey(x => x.Id);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<MediaAsset>()
                .WithMany()
                .HasForeignKey(x => x.MediaAssetId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(x => x.MediaUrl)
                .HasMaxLength(2048)
                .IsRequired();

            entity.Property(x => x.MediaType)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(x => x.Caption)
                .HasMaxLength(500);

            entity.HasIndex(x => new
            {
                x.UserId,
                x.ExpiresAtUtc
            });

            entity.HasIndex(x => new
            {
                x.IsDeleted,
                x.ExpiresAtUtc
            });
            entity.HasIndex(x => x.MediaAssetId).IsUnique();
        });

        // StoryView
        modelBuilder.Entity<StoryView>(entity =>
        {
            entity.ToTable("story_views");

            entity.HasKey(x => x.Id);

            entity.HasOne(x => x.Story)
                .WithMany(x => x.Views)
                .HasForeignKey(x => x.StoryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.ViewerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Aynı kullanıcı aynı story için tek görüntülenme kaydı.
            entity.HasIndex(x => new
            {
                x.StoryId,
                x.ViewerId
            })
            .IsUnique();
        });

    }
}
