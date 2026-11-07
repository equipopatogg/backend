using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Pactogg.Api.Common;
using Pactogg.Api.Common.Extensions;
using Pactogg.Api.Infrastructure.Audit;
using Pactogg.Api.Modules.Users;
using Pactogg.Api.Modules.Users.Dtos;

namespace Pactogg.Api.Modules.Devices;

[BsonIgnoreExtraElements]
public sealed class Device
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("user_id")]
    public ObjectId UserId { get; set; }

    /// <summary>Token de Firebase Cloud Messaging. Único: si cambia de dueño (otra cuenta en el mismo celular) se reasigna.</summary>
    [BsonElement("fcm_token")]
    public string FcmToken { get; set; } = string.Empty;

    [BsonElement("platform")]
    public string Platform { get; set; } = "android";

    [BsonElement("app_version")]
    public string? AppVersion { get; set; }

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

public interface IDeviceRepository
{
    Task<Device> UpsertAsync(ObjectId userId, string fcmToken, string platform, string? appVersion, DateTime now, CancellationToken cancellationToken);
    Task<long> DeleteAsync(ObjectId userId, string? fcmToken, CancellationToken cancellationToken);
    Task<IReadOnlyList<Device>> ListByUsersAsync(IReadOnlyCollection<ObjectId> userIds, CancellationToken cancellationToken);
    Task DeleteTokensAsync(IReadOnlyCollection<string> fcmTokens, CancellationToken cancellationToken);
}

public sealed class RegisterDeviceRequest
{
    [Required, StringLength(4096, MinimumLength = 20)]
    public string FcmToken { get; set; } = string.Empty;

    [Required, RegularExpression("^(android|ios|web)$")]
    public string Platform { get; set; } = "android";

    [StringLength(32)]
    public string? AppVersion { get; set; }
}

public sealed class DeviceResponse
{
    public string Id { get; init; } = string.Empty;
    public string Platform { get; init; } = string.Empty;
    public string? AppVersion { get; init; }
    public DateTime UpdatedAt { get; init; }
}

/// <summary>DEV-01..03: dispositivos para push y preferencias de avisos.</summary>
public sealed class DeviceService
{
    public const int MaxDevicesPerUser = 5;
    private readonly IDeviceRepository _devices;
    private readonly IAuditService _audit;

    public DeviceService(IDeviceRepository devices, IAuditService audit)
    {
        _devices = devices;
        _audit = audit;
    }

    public async Task<DeviceResponse> RegisterAsync(ObjectId userId, RegisterDeviceRequest request, CancellationToken cancellationToken)
    {
        var device = await _devices.UpsertAsync(userId, request.FcmToken.Trim(), request.Platform, request.AppVersion?.Trim(), DateTime.UtcNow, cancellationToken);

        // 006-R2 / CLARIFICACION #121: hasta 5 dispositivos por cuenta; al registrar el sexto se quita el que lleva más tiempo sin usarse (006-D1).
        var mine = await _devices.ListByUsersAsync([userId], cancellationToken);
        if (mine.Count > MaxDevicesPerUser)
        {
            var stale = mine.Where(x => x.Id != device.Id)
                .OrderBy(x => x.UpdatedAt)
                .Take(mine.Count - MaxDevicesPerUser)
                .Select(x => x.FcmToken)
                .ToList();
            await _devices.DeleteTokensAsync(stale, cancellationToken);
        }

        await _audit.AppendAsync("device.registered", userId, "device", device.Id, null, new { platform = device.Platform }, cancellationToken);
        return new DeviceResponse
        {
            Id = device.Id.ToString(),
            Platform = device.Platform,
            AppVersion = device.AppVersion,
            UpdatedAt = device.UpdatedAt
        };
    }

    /// <summary>Sin token borra todos los dispositivos del usuario (cerrar sesión en todos lados).</summary>
    public async Task RemoveAsync(ObjectId userId, string? fcmToken, CancellationToken cancellationToken)
    {
        var removed = await _devices.DeleteAsync(userId, string.IsNullOrWhiteSpace(fcmToken) ? null : fcmToken.Trim(), cancellationToken);
        await _audit.AppendAsync("device.removed", userId, "device", null, null, new { removed }, cancellationToken);
    }
}

public static class DeviceEndpoints
{
    public static IEndpointRouteBuilder MapDeviceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/devices/me").WithTags("Devices").RequireAuthorization();

        group.MapPut("", async (RegisterDeviceRequest request, DeviceService service, HttpContext context, CancellationToken cancellationToken) =>
                ApiResults.Ok(await service.RegisterAsync(context.User.GetRequiredObjectId(), request, cancellationToken)))
            .AddEndpointFilter<ValidationFilter<RegisterDeviceRequest>>()
            .WithName("RegisterDevice")
            .Produces<DeviceResponse>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapDelete("", async (string? fcm_token, DeviceService service, HttpContext context, CancellationToken cancellationToken) =>
            {
                await service.RemoveAsync(context.User.GetRequiredObjectId(), fcm_token, cancellationToken);
                return Results.NoContent();
            })
            .WithName("RemoveDevice")
            .Produces(StatusCodes.Status204NoContent);

        group.MapGet("/preferences", async (UserService users, HttpContext context, CancellationToken cancellationToken) =>
                ApiResults.Ok((await users.GetMeAsync(context.User.GetRequiredObjectId(), cancellationToken)).NotificationPreferences))
            .WithName("GetNotificationPreferences")
            .Produces<NotificationPreferencesDto>();

        group.MapPatch("/preferences", async (NotificationPreferencesDto request, UserService users, HttpContext context, CancellationToken cancellationToken) =>
                ApiResults.Ok(await users.UpdatePreferencesAsync(context.User.GetRequiredObjectId(), request, cancellationToken)))
            .WithName("UpdateNotificationPreferences")
            .Produces<NotificationPreferencesDto>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
