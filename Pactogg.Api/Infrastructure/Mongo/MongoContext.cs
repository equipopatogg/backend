using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Pactogg.Api.Infrastructure.Options;
using Pactogg.Api.Modules.Auth.Domain;
using Pactogg.Api.Modules.Contracts.Domain;
using Pactogg.Api.Modules.Evidence.Domain;
using Pactogg.Api.Modules.Groups.Domain;
using Pactogg.Api.Modules.Points.Domain;
using Pactogg.Api.Modules.Votes.Domain;
using Pactogg.Api.Modules.Commitments.Domain;
using Pactogg.Api.Modules.Notifications.Domain;
using Pactogg.Api.Modules.Tournaments.Domain;
using Pactogg.Api.Infrastructure.Audit;

namespace Pactogg.Api.Infrastructure.Mongo;

public sealed class MongoContext
{
    public MongoContext(IOptions<MongoOptions> options)
    {
        var value = options.Value;
        var connectionString = string.IsNullOrWhiteSpace(value.ConnectionString)
            ? "mongodb://localhost:27017/?serverSelectionTimeoutMS=2000"
            : value.ConnectionString;

        var settings = MongoClientSettings.FromConnectionString(connectionString);
        settings.MaxConnectionPoolSize = Math.Max(1, value.MaxConnectionPoolSize);
        Client = new MongoClient(settings);
        Database = Client.GetDatabase(value.DatabaseName);
    }

    public IMongoClient Client { get; }
    public IMongoDatabase Database { get; }

    public IMongoCollection<User> Users => Database.GetCollection<User>("users");
    public IMongoCollection<RefreshToken> RefreshTokens => Database.GetCollection<RefreshToken>("refresh_tokens");
    public IMongoCollection<Group> Groups => Database.GetCollection<Group>("groups");
    public IMongoCollection<GroupMember> GroupMembers => Database.GetCollection<GroupMember>("group_members");
    public IMongoCollection<Contract> Contracts => Database.GetCollection<Contract>("contracts");
    public IMongoCollection<Evidence> Evidences => Database.GetCollection<Evidence>("evidence");
    public IMongoCollection<Tournament> Tournaments => Database.GetCollection<Tournament>("tournaments");
    public IMongoCollection<Match> Matches => Database.GetCollection<Match>("matches");
    public IMongoCollection<PointsLedgerEntry> PointsLedger => Database.GetCollection<PointsLedgerEntry>("points_ledger");
    public IMongoCollection<Vote> Votes => Database.GetCollection<Vote>("votes");
    public IMongoCollection<Commitment> Commitments => Database.GetCollection<Commitment>("commitments");
    public IMongoCollection<Notification> Notifications => Database.GetCollection<Notification>("notifications");
    public IMongoCollection<AuditEvent> AuditEvents => Database.GetCollection<AuditEvent>("audit_events");
}
