using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;

using Microsoft.EntityFrameworkCore;

using Microsoft.IdentityModel.Tokens;

using Microsoft.OpenApi.Models;

using MindMatch.Application.Interfaces;

using MindMatch.Application.Services;

using MindMatch.Infrastructure.Persistence;

using MindMatch.Infrastructure.Repositories;

using MindMatch.Infrastructure.Security;
using MindMatch.Api.Services;
using MindMatch.Api.Hubs;
using MindMatch.Infrastructure.Storage;

using System.Text;
using System.Security.Claims;
using StackExchange.Redis;



var builder = WebApplication.CreateBuilder(args);



builder.Services.AddControllers();
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (allowedOrigins.Length == 0)
    throw new InvalidOperationException("En az bir Cors:AllowedOrigins değeri tanımlanmalıdır.");
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    policy.WithOrigins(allowedOrigins)
          .AllowAnyHeader()
          .AllowAnyMethod()
          .AllowCredentials();
}));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler(options =>
{
    options.ExceptionHandler = async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Beklenmeyen bir hata oluştu."
        });
    };
});
var redisConnection = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("Redis bağlantı ayarı bulunamadı.");
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));
builder.Services.AddSingleton<RedisPresenceTracker>();
builder.Services.AddSingleton<IUserPresenceReader>(sp => sp.GetRequiredService<RedisPresenceTracker>());
builder.Services.AddHostedService<RedisPresenceHeartbeat>();
builder.Services.AddSingleton<RedisRateLimiter>();
builder.Services.AddScoped<ActiveUserHubFilter>();
builder.Services.AddSignalR(options => options.AddFilter<ActiveUserHubFilter>()).AddStackExchangeRedis(redisConnection);
builder.Services.AddSingleton<IMessageEventPublisher, SignalRMessageEventPublisher>();
builder.Services.Configure<MediaStorageSettings>(builder.Configuration.GetSection("MediaStorage"));
builder.Services.AddSingleton<IMediaStorage, LocalMediaStorage>();
builder.Services.Configure<PasswordResetEmailSettings>(builder.Configuration.GetSection("PasswordResetEmail"));
builder.Services.AddSingleton<IPasswordResetEmailSender, SmtpPasswordResetEmailSender>();
builder.Services.Configure<TurnSettings>(builder.Configuration.GetSection("Turn"));
builder.Services.AddHttpClient<TurnCredentialService>();

builder.Services.AddSwaggerGen(options =>

{

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme

    {

        Name = "Authorization",

        Type = SecuritySchemeType.Http,

        Scheme = "bearer",

        BearerFormat = "JWT",

        In = ParameterLocation.Header,

        Description = "Login endpoint'inden aldığın access token'ı buraya gir."

    });



    options.AddSecurityRequirement(new OpenApiSecurityRequirement

    {

        {

            new OpenApiSecurityScheme

            {

                Reference = new OpenApiReference

                {

                    Type = ReferenceType.SecurityScheme,

                    Id = "Bearer"

                }

            },

            Array.Empty<string>()

        }

    });

});



// PostgreSQL

var connectionString =

    builder.Configuration.GetConnectionString("DefaultConnection")

    ?? throw new InvalidOperationException(

        "DefaultConnection ayarı bulunamadı.");



builder.Services.AddDbContext<MindMatchDbContext>(options =>

    options.UseNpgsql(connectionString));



// Unit of Work

builder.Services.AddScoped<IUnitOfWork>(

    sp => sp.GetRequiredService<MindMatchDbContext>());



// Repository kayıtları

builder.Services.AddScoped<IInterestRepository, InterestRepository>();

builder.Services.AddScoped<IMatchRepository, MatchRepository>();

builder.Services.AddScoped<IMoodCheckInRepository, MoodCheckInRepository>();

builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

builder.Services.AddScoped<IUserProfileRepository, UserProfileRepository>();

builder.Services.AddScoped<IUserRepository, UserRepository>();

builder.Services.AddScoped<IUserInterestRepository, UserInterestRepository>();
builder.Services.AddScoped<IUserBlockRepository, UserBlockRepository>();
builder.Services.AddScoped<IUserFollowRepository, UserFollowRepository>();
builder.Services.AddScoped<IReportRepository, ReportRepository>();
builder.Services.AddScoped<IMessageRequestRepository, MessageRequestRepository>();
builder.Services.AddScoped<ICallRepository, CallRepository>();
builder.Services.AddScoped<IMediaAssetRepository, MediaAssetRepository>();
builder.Services.AddScoped<IProfilePhotoRepository, ProfilePhotoRepository>();
builder.Services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
builder.Services.AddScoped<IPremiumEntitlementRepository, PremiumEntitlementRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IStoryRepository, StoryRepository>();
builder.Services.AddScoped<IUserMusicPreferenceRepository, UserMusicPreferenceRepository>();
builder.Services.AddScoped<IRealtimeOutboxRepository, RealtimeOutboxRepository>();



builder.Services.AddScoped<

    IConversationRepository,

    ConversationRepository>();

builder.Services.AddScoped<IMessageService, MessageService>();

builder.Services.AddScoped<IMessageRepository, MessageRepository>();



builder.Services.AddScoped<

    IConversationService,

    ConversationService>();





// Application servisleri

builder.Services.AddScoped<IInterestService, InterestService>();

builder.Services.AddScoped<IMatchService, MatchService>();

builder.Services.AddScoped<IMoodCheckInService, MoodCheckInService>();

builder.Services.AddScoped<IUserProfileService, UserProfileService>();
builder.Services.AddScoped<IDiscoveryService, DiscoveryService>();

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserBlockService, UserBlockService>();
builder.Services.AddScoped<IUserFollowService, UserFollowService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IMessageRequestService, MessageRequestService>();
builder.Services.AddScoped<ICallService, CallService>();
builder.Services.AddScoped<IMediaService, MediaService>();
builder.Services.AddScoped<IProfilePhotoService, ProfilePhotoService>();
builder.Services.AddScoped<IStoryService, StoryService>();
builder.Services.AddScoped<IPremiumService, PremiumService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<MusicPreferenceService>();
builder.Services.AddScoped<IBackgroundCleanupService, BackgroundCleanupService>();
builder.Services.AddHostedService<ExpiredContentCleanupWorker>();
builder.Services.AddHostedService<RealtimeOutboxWorker>();



// Güvenlik servisleri

builder.Services.AddScoped<IPasswordHasher, PasswordHasherService>();

builder.Services.AddScoped<ITokenService, TokenService>();

builder.Services.Configure<TokenSettings>(

    builder.Configuration.GetSection("Jwt"));



// JWT ayarları

var jwt = builder.Configuration.GetSection("Jwt");



var issuer = jwt["Issuer"]

    ?? throw new InvalidOperationException("Jwt:Issuer bulunamadı.");



var audience = jwt["Audience"]

    ?? throw new InvalidOperationException("Jwt:Audience bulunamadı.");



var key = jwt["SecretKey"]

    ?? throw new InvalidOperationException("Jwt:SecretKey bulunamadı.");



if (Encoding.UTF8.GetByteCount(key) < 32 ||

    key.StartsWith("BURAYA_", StringComparison.Ordinal))

{

    throw new InvalidOperationException(

        "Jwt:SecretKey için en az 32 baytlık güvenli bir anahtar gerekli.");

}



builder.Services

    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)

    .AddJwtBearer(options =>

    {

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    (context.HttpContext.Request.Path.StartsWithSegments("/hubs/messages") ||
                     context.HttpContext.Request.Path.StartsWithSegments("/hubs/calls")))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                var value = context.Principal?.FindFirstValue("sub")
                    ?? context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!Guid.TryParse(value, out var userId))
                {
                    context.Fail("Token kullanıcı kimliği içermiyor.");
                    return;
                }

                var users = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
                var user = await users.GetByIdAsync(userId, context.HttpContext.RequestAborted);
                var versionValue = context.Principal?.FindFirstValue("tv");
                if (!int.TryParse(versionValue, out var tokenVersion) || user is null || !user.IsActive || user.SessionVersion != tokenVersion)
                    context.Fail("Hesap etkin değil.");
            }
        };
        options.TokenValidationParameters = new TokenValidationParameters

        {

            ValidateIssuer = true,

            ValidIssuer = issuer,

            ValidateAudience = true,

            ValidAudience = audience,

            ValidateIssuerSigningKey = true,

            IssuerSigningKey = new SymmetricSecurityKey(

                Encoding.UTF8.GetBytes(key)),

            ValidateLifetime = true,

            ClockSkew = TimeSpan.FromSeconds(30)

        };

    });



builder.Services.AddAuthorization();



var app = builder.Build();



if (app.Environment.IsDevelopment())

{

  

    app.UseSwagger();

    app.UseSwaggerUI();

}



// app.UseHttpsRedirection(); // Disabled for local development to prevent SSL errors on localhost:5193
app.UseExceptionHandler();
app.UseCors();

app.UseAuthentication();

app.UseAuthorization();
app.UseMiddleware<RedisRateLimitMiddleware>();



app.MapControllers();
app.MapHub<MessagesHub>("/hubs/messages");
app.MapHub<CallSignalingHub>("/hubs/calls");



app.Run();

