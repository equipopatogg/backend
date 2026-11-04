using System.ComponentModel.DataAnnotations;
using Pactogg.Api.Common;

namespace Pactogg.Api.Common;

public sealed class ValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var argument = context.Arguments.OfType<T>().FirstOrDefault();
        if (argument is null)
        {
            return await next(context);
        }

        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(argument, new ValidationContext(argument), results, validateAllProperties: true))
        {
            var details = results
                .Select(result => new
                {
                    field = result.MemberNames.FirstOrDefault() ?? "body",
                    message = result.ErrorMessage ?? "Valor inválido."
                })
                .ToArray();
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "Datos inválidos.", details);
        }

        return await next(context);
    }
}
