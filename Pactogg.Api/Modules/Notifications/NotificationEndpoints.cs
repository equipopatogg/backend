using Pactogg.Api.Common;
using Pactogg.Api.Common.Extensions;
using Pactogg.Api.Modules.Notifications.Dtos;

namespace Pactogg.Api.Modules.Notifications;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/notifications").WithTags("Notifications").RequireAuthorization();

        group.MapGet("", async (
                bool? unread,
                int? limit,
                string? cursor,
                NotificationService service,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var (items, meta) = await service.ListAsync(context.User.GetRequiredObjectId(), unread ?? false, limit ?? 20, cursor, cancellationToken);
                return ApiResults.Ok(items, meta);
            })
            .WithName("ListNotifications")
            .Produces<IReadOnlyList<NotificationResponse>>();

        group.MapPatch("/{id}/read", async (string id, NotificationService service, HttpContext context, CancellationToken cancellationToken) =>
                ApiResults.Ok(await service.MarkReadAsync(context.User.GetRequiredObjectId(), id, cancellationToken)))
            .WithName("MarkNotificationRead")
            .Produces<NotificationResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/read-all", async (NotificationService service, HttpContext context, CancellationToken cancellationToken) =>
                ApiResults.Ok(await service.MarkAllReadAsync(context.User.GetRequiredObjectId(), cancellationToken)))
            .WithName("MarkAllNotificationsRead")
            .Produces<ReadAllResponse>();

        return app;
    }
}
