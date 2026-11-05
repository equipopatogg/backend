namespace Pactogg.Api.Modules.Notifications.Dtos;

public sealed class NotificationResponse
{
    public string Id { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public string? GroupId { get; init; }
    public string? ContractId { get; init; }
    public string? CommitmentId { get; init; }
    public bool Read { get; init; }
    public DateTime? ReadAt { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class ReadAllResponse
{
    public long Updated { get; init; }
}
