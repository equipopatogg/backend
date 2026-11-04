using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Pactogg.Api.Infrastructure.Mongo;
using Pactogg.Api.Infrastructure.Options;
using Testcontainers.MongoDb;

namespace DatabaseTest;

public sealed class MongoIndexInitializerTests
{
    [DockerFact]
    public async Task Initializer_CreatesUniqueEmailIndex()
    {
        var container = new MongoDbBuilder("mongo:7.0").Build();

        try
        {
            await container.StartAsync();

            var context = new MongoContext(Options.Create(new MongoOptions
            {
                ConnectionString = container.GetConnectionString(),
                DatabaseName = "pactogg_test"
            }));
            var initializer = new MongoIndexInitializer(context, NullLogger<MongoIndexInitializer>.Instance);

            await initializer.StartAsync(CancellationToken.None);
            try
            {
                var names = new List<string>();
                var deadline = DateTime.UtcNow.AddSeconds(30);
                while (DateTime.UtcNow < deadline)
                {
                    var indexes = await context.Users.Indexes.List().ToListAsync();
                    names = indexes.Select(index => index["name"].AsString).ToList();
                    if (names.Contains("ux_users_email"))
                    {
                        break;
                    }

                    await Task.Delay(250);
                }

                names.Should().Contain("ux_users_email");
            }
            finally
            {
                await initializer.StopAsync(CancellationToken.None);
            }
        }
        finally
        {
            await container.DisposeAsync();
        }
    }
}

public sealed class DockerFactAttribute : FactAttribute
{
    public DockerFactAttribute()
    {
        if (!File.Exists("/var/run/docker.sock") && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOCKER_HOST")))
        {
            Skip = "Docker no está disponible.";
        }
    }
}
