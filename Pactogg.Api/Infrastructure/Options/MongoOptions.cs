namespace Pactogg.Api.Infrastructure.Options;

public sealed class MongoOptions
{
    public const string SectionName = "MongoDb";

    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = "pactogg";
    public int MaxConnectionPoolSize { get; set; } = 20;
}
