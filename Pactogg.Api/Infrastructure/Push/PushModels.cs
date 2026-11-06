using System.Threading.Channels;
using MongoDB.Bson;

namespace Pactogg.Api.Infrastructure.Push;

public sealed record PushMessage(IReadOnlyCollection<ObjectId> UserIds, string Type, string Title, string Body, IReadOnlyDictionary<string, string> Data);

public sealed record PushResult(int Sent, IReadOnlyCollection<string> InvalidTokens);

/// <summary>Envía a tokens concretos. FCM en producción; vacío si no hay credenciales.</summary>
public interface IPushSender
{
    bool IsConfigured { get; }
    Task<PushResult> SendAsync(IReadOnlyCollection<string> tokens, PushMessage message, CancellationToken cancellationToken);
}

/// <summary>Cola en memoria: la petición HTTP no espera a FCM (NOT-05 es interno y "mejor esfuerzo").</summary>
public sealed class PushQueue
{
    private readonly Channel<PushMessage> _channel = Channel.CreateBounded<PushMessage>(
        new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    public bool TryEnqueue(PushMessage message) => _channel.Writer.TryWrite(message);

    public ChannelReader<PushMessage> Reader => _channel.Reader;
}

public sealed class FirebaseOptions
{
    public const string SectionName = "Firebase";

    public string? ProjectId { get; set; }

    /// <summary>JSON de la cuenta de servicio. Secreto: Parameter Store (/pactogg/firebase/service-account).</summary>
    public string? ServiceAccountJson { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ServiceAccountJson);
}
