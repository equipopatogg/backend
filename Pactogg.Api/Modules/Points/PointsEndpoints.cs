using Pactogg.Api.Common;
using Pactogg.Api.Common.Extensions;
using Pactogg.Api.Modules.Points.Dtos;

namespace Pactogg.Api.Modules.Points;

public static class PointsEndpoints
{
    public static IEndpointRouteBuilder MapPointsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/groups/{gid}/points").WithTags("Points").RequireAuthorization();

        group.MapGet("/me", async (
                string gid,
                string? type,
                int? limit,
                string? cursor,
                PointsService service,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var (data, meta) = await service.GetMineAsync(context.User.GetRequiredObjectId(), gid, type, limit ?? 20, cursor, cancellationToken);
                return ApiResults.Ok(data, meta);
            })
            .WithName("GetMyPoints")
            .Produces<MyPointsResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/ranking", async (string gid, int? limit, PointsService service, HttpContext context, CancellationToken cancellationToken) =>
                ApiResults.Ok(await service.RankingAsync(context.User.GetRequiredObjectId(), gid, limit ?? 20, cancellationToken)))
            .WithName("GetPointsRanking")
            .Produces<IReadOnlyList<RankingEntryResponse>>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }
}
