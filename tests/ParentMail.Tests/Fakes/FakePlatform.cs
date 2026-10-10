using ParentMail.Core;

namespace ParentMail.Tests.Fakes;

// 基盤サービスの手書きフェイク（資格情報・時計・ログ・アドレス帳照会）。

public sealed class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public DateTime LocalNow => UtcNow.ToLocalTime();
    public void Advance(TimeSpan span) => UtcNow += span;
}

public sealed class FakeAppLog : IAppLog
{
    public List<string> Entries { get; } = new();
    public void Info(string message) => Entries.Add("I:" + message);
    public void Warn(string message) => Entries.Add("W:" + message);
    public void Error(string message, Exception? ex = null) => Entries.Add("E:" + message);
}

public sealed class FakeCredentialStore : ICredentialStore
{
    private readonly Dictionary<string, string> _items = new();
    public void Save(string accountId, string appPassword) => _items[accountId] = appPassword;
    public string? Get(string accountId) => _items.GetValueOrDefault(accountId);
    public void Delete(string accountId) => _items.Remove(accountId);
}

public sealed class FakeContactLookup : IContactLookup
{
    public HashSet<string> Known { get; } = new(StringComparer.OrdinalIgnoreCase);
    public bool IsKnown(string accountId, string address) => Known.Contains(address);
}
