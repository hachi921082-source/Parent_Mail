namespace ParentMail.Core.Features.MailDisplay;

/// <summary>
/// 一覧(mail-list)がプレビューに伝える補助情報。
/// ・アプリ側が自動で選び直したとき（削除・移動・再読み込みのあと）は、既読にしない（A58, Q74）。
/// ・一覧のチェックが入っている数（複数選択の案内表示用、A60）。
/// </summary>
public interface IPreviewSelectionHints
{
    /// <summary>自動選択の間だけ「自動」になる。SelectedMessageId を変える処理を using で囲む。</summary>
    IDisposable BeginAutoSelection();

    /// <summary>いま自動選択の最中なら true。選択変更イベントの中（同期呼び出し）で読む。</summary>
    bool IsAutoSelection { get; }

    /// <summary>一覧でチェックされている行の数。</summary>
    int CheckedCount { get; set; }

    event EventHandler? CheckedCountChanged;
}

public sealed class PreviewSelectionHints : IPreviewSelectionHints
{
    private int _autoDepth;
    private int _checked;

    public IDisposable BeginAutoSelection()
    {
        Interlocked.Increment(ref _autoDepth);
        return new Scope(this);
    }

    public bool IsAutoSelection => Volatile.Read(ref _autoDepth) > 0;

    public int CheckedCount
    {
        get => _checked;
        set
        {
            var v = Math.Max(0, value);
            if (v == _checked) return;
            _checked = v;
            CheckedCountChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? CheckedCountChanged;

    private sealed class Scope(PreviewSelectionHints owner) : IDisposable
    {
        private int _disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0) Interlocked.Decrement(ref owner._autoDepth);
        }
    }
}
