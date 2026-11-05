using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.Comprehend;
using Amazon.Rekognition;
using Amazon.S3;
using Amazon;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Pactogg.Api.Common;
using Pactogg.Api.Common.Middleware;
using Pactogg.Api.Infrastructure.Audit;
using Pactogg.Api.Infrastructure.Moderation;
using Pactogg.Api.Infrastructure.Mongo;
using Pactogg.Api.Infrastructure.Mongo.Repositories;
using Pactogg.Api.Infrastructure.Options;
using Pactogg.Api.Infrastructure.Redis;
using Pactogg.Api.Infrastructure.Security;
using Pactogg.Api.Infrastructure.Storage;
using Pactogg.Api.Modules.Auth;
using Pactogg.Api.Modules.Auth.Abstractions;
using Pactogg.Api.Modules.Contracts;
using Pactogg.Api.Modules.Contracts.Abstractions;
using Pactogg.Api.Modules.Evidence;
using Pactogg.Api.Modules.Evidence.Abstractions;
using Pactogg.Api.Modules.Groups;
using Pactogg.Api.Modules.Groups.Abstractions;
using Pactogg.Api.Modules.Users;
using Pactogg.Api.Modules.Votes;
using Pactogg.Api.Modules.Votes.Abstractions;
using Pactogg.Api.Modules.Commitments;
using Pactogg.Api.Modules.Commitments.Abstractions;
using Pactogg.Api.Modules.Notifications;
using Pactogg.Api.Modules.Notifications.Abstractions;
using Pactogg.Api.Modules.Points;
using Pactogg.Api.Modules.Points.Abstractions;
using Pactogg.Api.Modules.Fame;
using Pactogg.Api.Modules.Tournaments;
using Pactogg.Api.Modules.Tournaments.Abstractions;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile(
    $"appsettings.{builder.Environment.EnvironmentName}.local.json",
    optional: true,
    reloadOnChange: false);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);

builder.Host.UseSerilog((context, _, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console());

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 6 * 1024 * 1024;
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 6 * 1024 * 1024;
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
});

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey))
{
    if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
    {
        var generated = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        builder.Configuration["Jwt:SigningKey"] = generated;
        jwtOptions.SigningKey = generated;
    }
    else
    {
        throw new InvalidOperationException("Configura Jwt:SigningKey con al menos 32 bytes antes de arrancar en producción.");
    }
}

if (Encoding.UTF8.GetByteCount(jwtOptions.SigningKey) < 32)
{
    throw new InvalidOperationException("Jwt:SigningKey debe tener al menos 32 bytes.");
}

builder.Services.Configure<MongoOptions>(builder.Configuration.GetSection(MongoOptions.SectionName));
builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection(RedisOptions.SectionName));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.Configure<EvidenceOptions>(builder.Configuration.GetSection(EvidenceOptions.SectionName));
builder.Services.Configure<AwsOptions>(builder.Configuration.GetSection(AwsOptions.SectionName));
builder.Services.Configure<ModerationOptions>(builder.Configuration.GetSection(ModerationOptions.SectionName));
builder.Services.Configure<RateLimitOptions>(builder.Configuration.GetSection(RateLimitOptions.SectionName));

if (builder.Environment.IsProduction()
    && !string.Equals(builder.Configuration["Moderation:Mode"], "Aws", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException("En producción Moderation:Mode debe ser Aws; la moderación local solo se permite en desarrollo y pruebas.");
}

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
        }
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "sub",
            RoleClaimType = "role"
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var jti = context.Principal?.FindFirst("jti")?.Value;
                if (string.IsNullOrWhiteSpace(jti))
                {
                    context.Fail("missing_jti");
                    return;
                }

                var revocations = context.HttpContext.RequestServices.GetRequiredService<ITokenRevocationStore>();
                if (await revocations.IsRevokedAsync(jti, context.HttpContext.RequestAborted))
                {
                    context.Fail("revoked");
                }
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                await ErrorResponseWriter.WriteAsync(
                    context.HttpContext,
                    StatusCodes.Status401Unauthorized,
                    ErrorCodes.Unauthorized,
                    "Autenticación requerida.",
                    cancellationToken: context.HttpContext.RequestAborted);
            },
            OnForbidden = async context =>
            {
                await ErrorResponseWriter.WriteAsync(
                    context.HttpContext,
                    StatusCodes.Status403Forbidden,
                    ErrorCodes.Forbidden,
                    "No tienes permiso para esta operación.",
                    cancellationToken: context.HttpContext.RequestAborted);
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PactoGG API",
        Version = "v1",
        Description = "Backend de retos, grupos y torneos de PactoGG."
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT de acceso de PactoGG."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = "Bearer"
            }
        }] = Array.Empty<string>()
    });
});

builder.Services.AddHealthChecks();

builder.Services.AddSingleton<MongoContext>();
builder.Services.AddHostedService<MongoIndexInitializer>();
builder.Services.AddSingleton<RedisConnection>();
builder.Services.AddSingleton<ICacheService, RedisCacheService>();
builder.Services.AddSingleton<IRateLimitStore, RedisRateLimitStore>();
builder.Services.AddSingleton<ITokenRevocationStore, RedisTokenRevocationStore>();
builder.Services.AddSingleton<ILoginAttemptStore, RedisLoginAttemptStore>();
builder.Services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<IAuditService, MongoAuditService>();
builder.Services.AddSingleton<IObjectStorage, S3ObjectStorage>();

builder.Services.AddSingleton<IAmazonS3>(_ =>
    new AmazonS3Client(RegionEndpoint.GetBySystemName(
        builder.Configuration.GetSection(AwsOptions.SectionName).Get<AwsOptions>()?.Region ?? "us-east-1")));
builder.Services.AddSingleton<IAmazonRekognition>(_ =>
    new AmazonRekognitionClient(RegionEndpoint.GetBySystemName(
        builder.Configuration.GetSection(AwsOptions.SectionName).Get<AwsOptions>()?.Region ?? "us-east-1")));
builder.Services.AddSingleton<IAmazonComprehend>(_ =>
    new AmazonComprehendClient(RegionEndpoint.GetBySystemName(
        builder.Configuration.GetSection(AwsOptions.SectionName).Get<AwsOptions>()?.Region ?? "us-east-1")));

builder.Services.AddSingleton<IModerationService>(services =>
{
    var mode = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<ModerationOptions>>().Value.Mode;
    return string.Equals(mode, "Aws", StringComparison.OrdinalIgnoreCase)
        ? ActivatorUtilities.CreateInstance<AwsModerationService>(services)
        : ActivatorUtilities.CreateInstance<LocalModerationService>(services);
});

builder.Services.AddScoped<IUserRepository, MongoUserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, MongoRefreshTokenRepository>();
builder.Services.AddScoped<IGroupRepository, MongoGroupRepository>();
builder.Services.AddScoped<IGroupMemberRepository, MongoGroupMemberRepository>();
builder.Services.AddScoped<IContractRepository, MongoContractRepository>();
builder.Services.AddScoped<IEvidenceRepository, MongoEvidenceRepository>();
builder.Services.AddScoped<ITournamentRepository, MongoTournamentRepository>();
builder.Services.AddScoped<IMatchRepository, MongoMatchRepository>();
builder.Services.AddScoped<IVoteRepository, MongoVoteRepository>();
builder.Services.AddScoped<ICommitmentRepository, MongoCommitmentRepository>();
builder.Services.AddScoped<INotificationRepository, MongoNotificationRepository>();
builder.Services.AddScoped<IPointsLedgerRepository, MongoPointsLedgerRepository>();

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<GroupService>();
builder.Services.AddScoped<ContractService>();
builder.Services.AddScoped<EvidenceService>();
builder.Services.AddScoped<TournamentService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<GroupAccess>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<VoteService>();
builder.Services.AddScoped<PointsService>();
builder.Services.AddScoped<CommitmentService>();
builder.Services.AddScoped<FameService>();

var app = builder.Build();

app.UseMiddleware<RequestIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<NoSqlKeyValidationMiddleware>();
app.UseCors("Default");

if (app.Environment.IsProduction())
{
    app.UseHsts();
}

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "PactoGG API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseAuthentication();
app.UseMiddleware<RedisRateLimitMiddleware>();
app.UseAuthorization();

app.MapGet("/", () => ApiResults.Ok(new
{
    service = "PactoGG API",
    version = "0.6.0",
    status = "ok"
})).AllowAnonymous();

app.MapHealthChecks("/health").AllowAnonymous();
app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapGroupEndpoints();
app.MapContractEndpoints();
app.MapEvidenceEndpoints();
app.MapTournamentEndpoints();
app.MapVoteEndpoints();
app.MapPointsEndpoints();
app.MapCommitmentEndpoints();
app.MapNotificationEndpoints();
app.MapFameEndpoints();

app.Run();

public partial class Program { }
