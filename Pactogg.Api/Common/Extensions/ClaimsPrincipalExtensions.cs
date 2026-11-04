using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using MongoDB.Bson;
using Pactogg.Api.Common;

namespace Pactogg.Api.Common.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static string GetRequiredUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue("sub");
        if (!ObjectId.TryParse(value, out _))
        {
            throw ApiException.Unauthorized();
        }

        return value!;
    }

    public static ObjectId GetRequiredObjectId(this ClaimsPrincipal principal) =>
        ObjectId.Parse(principal.GetRequiredUserId());
}
