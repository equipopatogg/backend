using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pactogg.Api.Infrastructure.Options;
using Pactogg.Api.Infrastructure.Redis;
using Pactogg.Api.Infrastructure.Security;

namespace UnitTest;

/// <summary>AUTH-04: revocación de tokens al cerrar sesión; en producción falla cerrada si Redis no responde.</summary>
public sealed class TokenRevocationTests
{
    private static RedisConnection WithoutRedis() =>
        new(Options.Create(new RedisOptions()), NullLogger<RedisConnection>.Instance);

    [Fact]
    public async Task RequiredRedis_Unavailable_TreatsEveryTokenAsRevoked()
    {
        var store = new RedisTokenRevocationStore(WithoutRedis(), Options.Create(new RedisOptions { Required = true }));

        (await store.IsRevokedAsync("jti-cualquiera", CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task LocalMode_RevokedTokenStaysRevokedUntilItExpires()
    {
        var store = new RedisTokenRevocationStore(WithoutRedis(), Options.Create(new RedisOptions()));

        (await store.IsRevokedAsync("jti-activo", CancellationToken.None)).Should().BeFalse();
        await store.RevokeAsync("jti-cerrado", DateTime.UtcNow.AddMinutes(5), CancellationToken.None);
        (await store.IsRevokedAsync("jti-cerrado", CancellationToken.None)).Should().BeTrue();
        await store.RevokeAsync("jti-vencido", DateTime.UtcNow.AddMinutes(-1), CancellationToken.None);
        (await store.IsRevokedAsync("jti-vencido", CancellationToken.None)).Should().BeFalse();
    }
}
