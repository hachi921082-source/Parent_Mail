namespace ParentMail.App.Features.Compose;

/// <summary>
/// 開いている作成窓の一覧（終了時の確認など、アプリ全体から作成窓を数えるために使う）。
/// 窓が閉じると自動で外れる。UI スレッドからだけ使う。
/// </summary>
public sealed class ComposeWindowRegistry
{
    private readonly List<ComposeWindow> _windows = new();

    public IReadOnlyList<ComposeWindow> Windows => _windows.ToList();
    public int Count => _windows.Count;

    /// <summary>窓が増えた・減ったとき。</summary>
    public event EventHandler? Changed;

    public void Register(ComposeWindow window)
    {
        _windows.Add(window);
        window.Closed += (_, _) =>
        {
            _windows.Remove(window);
            Changed?.Invoke(this, EventArgs.Empty);
        };
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
