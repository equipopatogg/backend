using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using Pactogg.Api.Common;
using Pactogg.Api.Common.Extensions;
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

                    if (file.Length <= 0 || file.Length > options.Value.MaxImageBytes)
                    {
                        throw ApiException.Unprocessable(ErrorCodes.ImageTooLarge, "La imagen supera el tamaño permitido.");
                    }

                    await using var stream = file.OpenReadStream();
                    using var memory = new MemoryStream();
                    await stream.CopyToAsync(memory, cancellationToken);
                    if (memory.Length > options.Value.MaxImageBytes)
                    {
                        throw ApiException.Unprocessable(ErrorCodes.ImageTooLarge, "La imagen supera el tamaño permitido.");
                    }

                    var photo = await service.AddPhotoAsync(userId, id, memory.ToArray(), file.ContentType, cancellationToken);
                    return ApiResults.Created(photo);
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

                var evidence = await service.AddLinkAsync(userId, id, request, cancellationToken);
                return ApiResults.Created(evidence);
            })
            .RequireAuthorization()
            .WithName("CreateEvidence")
            .Produces<EvidenceResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
