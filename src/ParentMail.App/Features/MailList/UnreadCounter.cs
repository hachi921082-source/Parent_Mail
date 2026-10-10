using ParentMail.Core;
using ParentMail.Core.Models;

namespace ParentMail.App.Features.MailList;

/// <summary>表示中のアカウントの受信トレイの未読数と、送信箱の失敗数を数える（上部バーのバッジ用、TB-07）。</summary>
public sealed class UnreadCounter
{
    private readonly IMailRepository _mail;
    private readonly IAppState _state;
    private readonly IOutboxService? _outbox;

    public UnreadCounter(IMailRepository mail, IAppState state, IOutboxService? outbox = null)
    {
        _mail = mail;
        _state = state;
        _outbox = outbox;
    }

    public int GetInboxUnread()
        => _state.CurrentAccountId is { } id ? _mail.UnreadCount(id, FolderKind.Inbox) : 0;

    public int GetOutboxFailed()
        => _state.CurrentAccountId is { } id && _outbox is not null ? _outbox.FailedCount(id) : 0;
}
