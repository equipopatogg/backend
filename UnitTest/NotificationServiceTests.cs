using FluentAssertions;
using IntegrationTest.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using Pactogg.Api.Common;
using Pactogg.Api.Infrastructure.Push;
using Pactogg.Api.Modules.Notifications;

namespace UnitTest;

/// <summary>NOT-01..05: reglas del centro de avisos con un repositorio en memoria (sin HTTP ni base de datos).</summary>
public sealed class NotificationServiceTests
{
    private readonly InMemoryNotificationRepository _repository = new();
    private readonly NotificationService _service;
    private readonly ObjectId _owner = ObjectId.GenerateNewId();
    private readonly ObjectId _other = ObjectId.GenerateNewId();

    public NotificationServiceTests()
    {
        _service = new NotificationService(_repository, NullLogger<NotificationService>.Instance, new PushQueue());
    }

    [Fact]
    public async Task Notify_TruncatesTextAndDoesNotDuplicateTheSameEvent()
    {
        var contract = ObjectId.GenerateNewId();
        await _service.NotifyAsync(new[] { _owner, _owner }, "contract_received", new string('t', 120), new string('b', 300),
            contractId: contract);
        await _service.NotifyAsync(new[] { _owner }, "contract_received", "otra vez", "mismo evento", contractId: contract);

        var saved = _repository.For(_owner).Should().ContainSingle().Which;
        saved.Title.Should().HaveLength(80).And.EndWith("…");
        saved.Body.Should().HaveLength(200);
    }

    [Fact]
    public async Task List_ReportsUnreadCountAndRejectsInvalidLimit()
    {
        await _service.NotifyAsync(new[] { _owner }, "a", "Uno", "x");
        await _service.NotifyAsync(new[] { _owner }, "b", "Dos", "x");

        var (items, _) = await _service.ListAsync(_owner, unreadOnly: true, limit: 1, cursor: null, CancellationToken.None);
        items.Should().HaveCount(1);

        var invalid = () => _service.ListAsync(_owner, false, 51, null, CancellationToken.None);
        (await invalid.Should().ThrowAsync<ApiException>()).Which.StatusCode.Should().Be(422);
    }

    [Fact]
    public async Task MarkRead_OnlyTheOwnerCanReadItsNotice()
    {
        await _service.NotifyAsync(new[] { _owner }, "a", "Uno", "x");
        var id = _repository.For(_owner).Single().Id.ToString();

        var foreign = () => _service.MarkReadAsync(_other, id, CancellationToken.None);
        (await foreign.Should().ThrowAsync<ApiException>()).Which.StatusCode.Should().Be(404);
        var malformed = () => _service.MarkReadAsync(_owner, "no-es-id", CancellationToken.None);
        (await malformed.Should().ThrowAsync<ApiException>()).Which.StatusCode.Should().Be(404);

        (await _service.MarkReadAsync(_owner, id, CancellationToken.None)).Read.Should().BeTrue();
    }

    [Fact]
    public async Task MarkAllRead_CountsOnlyTheOwnersUnreadNotices()
    {
        await _service.NotifyAsync(new[] { _owner, _other }, "a", "Uno", "x");
        await _service.NotifyAsync(new[] { _owner }, "b", "Dos", "x");

        (await _service.MarkAllReadAsync(_owner, CancellationToken.None)).Updated.Should().Be(2);
        (await _service.MarkAllReadAsync(_owner, CancellationToken.None)).Updated.Should().Be(0);
        _repository.For(_other).Single().ReadAt.Should().BeNull();
    }

    [Fact]
    public async Task Delete_OtherUsersNoticeIsNotFoundAndStaysSaved()
    {
        await _service.NotifyAsync(new[] { _owner }, "a", "Uno", "x");
        var id = _repository.For(_owner).Single().Id.ToString();

        var foreign = () => _service.DeleteAsync(_other, id, CancellationToken.None);
        (await foreign.Should().ThrowAsync<ApiException>()).Which.StatusCode.Should().Be(404);
        _repository.For(_owner).Should().HaveCount(1);

        await _service.DeleteAsync(_owner, id, CancellationToken.None);
        _repository.For(_owner).Should().BeEmpty();
    }
}
