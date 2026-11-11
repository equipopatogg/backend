using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using Pactogg.Api.Common;
using Pactogg.Api.Common.Extensions;
using Pactogg.Api.Common.Middleware;
using Pactogg.Api.Infrastructure.Options;
using Pactogg.Api.Modules.Evidence.Dtos;

namespace Pactogg.Api.Modules.Evidence;

public static class EvidenceEndpoints
{
    public static IEndpointRouteBuilder MapEvidenceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/contracts").WithTags("Evidence");

        group.MapPost("/{id}/evidence", async (
                string id,
                HttpContext context,
                EvidenceService service,
                IOptions<EvidenceOptions> options,
                CancellationToken cancellationToken) =>
            {
                var userId = context.User.GetRequiredObjectId();
                var upload = await ReadUploadAsync(context, options.Value, cancellationToken);
                var evidence = upload.Link is not null
                    ? await service.AddLinkAsync(userId, id, upload.Link, cancellationToken)
                    : await service.AddPhotoAsync(userId, id, upload.Content!, upload.ContentType!, cancellationToken);
                return ApiResults.Created(evidence);
            })
            .RequireAuthorization()
            .AllowIdempotencyKey()
            .WithName("CreateEvidence")
            .Produces<EvidenceResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/{id}/evidence", async (string id, EvidenceService service, HttpContext context, CancellationToken cancellationToken) =>
                ApiResults.Ok(await service.ListAsync(context.User.GetRequiredObjectId(), id, cancellationToken)))
            .RequireAuthorization()
            .WithName("ListEvidence")
            .Produces<IReadOnlyList<EvidenceResponse>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // EVI-04 (spec 007-R7): mismo cuerpo que EVI-02.
        group.MapPut("/{id}/evidence/{eid}", async (
                string id,
                string eid,
                HttpContext context,
                EvidenceService service,
                IOptions<EvidenceOptions> options,
                CancellationToken cancellationToken) =>
            {
                var upload = await ReadUploadAsync(context, options.Value, cancellationToken);
                return ApiResults.Ok(await service.ReplaceAsync(context.User.GetRequiredObjectId(), id, eid, upload, cancellationToken));
            })
            .RequireAuthorization()
            .WithName("ReplaceEvidence")
            .Produces<EvidenceResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        var evidence = app.MapGroup("/api/v1/evidence").WithTags("Evidence");

        // EVI-05 (spec 007-R8).
        evidence.MapDelete("/{eid}", async (string eid, HttpContext context, EvidenceService service, CancellationToken cancellationToken) =>
            {
                await service.DeleteAsync(context.User.GetRequiredObjectId(), eid, cancellationToken);
                return Results.NoContent();
            })
            .RequireAuthorization()
            .WithName("DeleteEvidence")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // EVI-06 (spec 007-R9).
        evidence.MapPost("/{eid}/save-fame", async (string eid, HttpContext context, EvidenceService service, CancellationToken cancellationToken) =>
                ApiResults.Ok(await service.SaveInFameAsync(context.User.GetRequiredObjectId(), eid, cancellationToken)))
            .RequireAuthorization()
            .WithName("SaveEvidenceInFame")
            .Produces<EvidenceResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }

    /// <summary>Lee una foto (multipart con <c>file</c>) o un enlace (JSON <c>{type:"link", url}</c>).</summary>
    private static async Task<EvidenceUpload> ReadUploadAsync(HttpContext context, EvidenceOptions options, CancellationToken cancellationToken)
    {
        if (context.Request.HasFormContentType)
        {
            IFormCollection form;
            try
            {
                form = await context.Request.ReadFormAsync(cancellationToken);
            }
            catch (InvalidDataException)
            {
                throw ApiException.Unprocessable(ErrorCodes.ImageTooLarge, "La imagen supera el tamaño permitido.");
            }

            var type = form["type"].ToString();
            if (!string.IsNullOrWhiteSpace(type) && !string.Equals(type, "photo", StringComparison.OrdinalIgnoreCase))
            {
                throw ApiException.Unprocessable(ErrorCodes.ValidationError, "type debe ser photo.");
            }

            var file = form.Files.GetFile("file")
                ?? throw ApiException.Unprocessable(ErrorCodes.ValidationError, "Falta el archivo file.");

            if (file.Length <= 0 || file.Length > options.MaxImageBytes)
            {
                throw ApiException.Unprocessable(ErrorCodes.ImageTooLarge, "La imagen supera el tamaño permitido.");
            }

            await using var stream = file.OpenReadStream();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, cancellationToken);
            if (memory.Length > options.MaxImageBytes)
            {
                throw ApiException.Unprocessable(ErrorCodes.ImageTooLarge, "La imagen supera el tamaño permitido.");
            }

            return new EvidenceUpload(memory.ToArray(), file.ContentType, null);
        }

        CreateLinkEvidenceRequest? request;
        try
        {
            request = await context.Request.ReadFromJsonAsync<CreateLinkEvidenceRequest>(cancellationToken: cancellationToken);
        }
        catch (System.Text.Json.JsonException)
        {
            throw ApiException.BadRequest(ErrorCodes.InvalidInput, "Cuerpo JSON inválido.");
        }

        if (request is null)
        {
            throw ApiException.BadRequest(ErrorCodes.InvalidInput, "Cuerpo JSON requerido.");
        }

        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true))
        {
            var details = results.Select(result => new
            {
                field = result.MemberNames.FirstOrDefault() ?? "body",
                message = result.ErrorMessage ?? "Valor inválido."
            });
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "Datos inválidos.", details);
        }

        return new EvidenceUpload(null, null, request);
    }
}
