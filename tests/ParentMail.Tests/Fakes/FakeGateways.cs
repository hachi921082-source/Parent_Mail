using ParentMail.Core;
using ParentMail.Core.Models;

namespace ParentMail.Tests.Fakes;

// IMAP / SMTP の手書きフェイク。メモリ上のフォルダで動き、呼び出し履歴を残す。

public sealed class FakeImapGateway : IImapGateway
{
    public LoginResult LoginResult { get; set; } = LoginResult.Ok;
    public List<ImapFolderInfo> Folders { get; } = new();
    public Dictionary<string, ImapFolderStatus> Statuses { get; } = new();
    public Dictionary<string, List<ImapHeader>> Headers { get; } = new();
    public Dictionary<(string Folder, uint Uid), ImapBody> Bodies { get; } = new();
    public List<string> Calls { get; } = new();
    /// <summary>true のとき全操作が IOException になる（オフライン再現）。</summary>
    public bool Offline { get; set; }

    private void Enter(string call)
    {
        Calls.Add(call);
        if (Offline) throw new IOException("offline");
    }

    public Task<LoginResult> TestLoginAsync(CancellationToken ct = default)
    {
        Enter("TestLogin");
        return Task.FromResult(LoginResult);
    }

    public Task<IReadOnlyList<ImapFolderInfo>> ListFoldersAsync(CancellationToken ct = default)
    {
        Enter("ListFolders");
        return Task.FromResult<IReadOnlyList<ImapFolderInfo>>(Folders.ToList());
    }

    public Task<ImapFolderStatus> GetFolderStatusAsync(string folderPath, CancellationToken ct = default)
    {
        Enter("GetFolderStatus:" + folderPath);
        return Task.FromResult(Statuses.TryGetValue(folderPath, out var s) ? s : new ImapFolderStatus(folderPath, 1, 1, null, 0));
    }

    public Task<IReadOnlyList<uint>> SearchUidsAsync(string folderPath, DateTime? sinceUtc, CancellationToken ct = default)
    {
        Enter("SearchUids:" + folderPath);
        var list = Headers.TryGetValue(folderPath, out var h)
            ? h.Where(x => sinceUtc is null || x.DateUtc >= sinceUtc).Select(x => x.Uid).ToList()
            : new List<uint>();
        return Task.FromResult<IReadOnlyList<uint>>(list);
    }

    public Task<IReadOnlyList<ImapHeader>> FetchHeadersAsync(string folderPath, IReadOnlyList<uint> uids, CancellationToken ct = default)
    {
        Enter("FetchHeaders:" + folderPath);
        var list = Headers.TryGetValue(folderPath, out var h) ? h.Where(x => uids.Contains(x.Uid)).ToList() : new List<ImapHeader>();
        return Task.FromResult<IReadOnlyList<ImapHeader>>(list);
    }

    public Task<ImapBody?> FetchBodyAsync(string folderPath, uint uid, CancellationToken ct = default)
    {
        Enter("FetchBody:" + folderPath + ":" + uid);
        return Task.FromResult(Bodies.TryGetValue((folderPath, uid), out var b) ? b : null);
    }

    public Task<Stream?> FetchPartAsync(string folderPath, uint uid, string partId, CancellationToken ct = default)
    {
        Enter("FetchPart:" + folderPath + ":" + uid + ":" + partId);
        return Task.FromResult<Stream?>(null);
    }

    public Task SetSeenAsync(string folderPath, IReadOnlyList<uint> uids, bool seen, CancellationToken ct = default)
    {
        Enter("SetSeen:" + folderPath + ":" + seen);
        return Task.CompletedTask;
    }

    public Task<uint?> MoveAsync(string fromFolderPath, uint uid, string toFolderPath, CancellationToken ct = default)
    {
        Enter("Move:" + fromFolderPath + "->" + toFolderPath);
        return Task.FromResult<uint?>(null);
    }

    public Task<uint?> AppendAsync(string folderPath, byte[] rawMessage, bool seen, CancellationToken ct = default)
    {
        Enter("Append:" + folderPath);
        return Task.FromResult<uint?>(1);
    }

    public Task DeleteAsync(string folderPath, IReadOnlyList<uint> uids, CancellationToken ct = default)
    {
        Enter("Delete:" + folderPath);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public sealed class FakeImapGatewayFactory : IImapGatewayFactory
{
    public Dictionary<string, FakeImapGateway> ByAccountId { get; } = new();

    public IImapGateway Create(Account account)
    {
        if (!ByAccountId.TryGetValue(account.Id, out var gw)) ByAccountId[account.Id] = gw = new FakeImapGateway();
        return gw;
    }
}

public sealed class FakeSmtpGateway : ISmtpGateway
{
    public List<(MimeKit.MimeMessage Message, string AccountId)> Sent { get; } = new();
    /// <summary>次回以降の送信結果。Ok 以外なら Sent には記録しない。</summary>
    public SendResult Result { get; set; } = SendResult.Ok;

    public Task<SendResult> SendAsync(MimeKit.MimeMessage message, string accountId, CancellationToken ct = default)
    {
        if (Result == SendResult.Ok) Sent.Add((message, accountId));
        return Task.FromResult(Result);
    }
}
