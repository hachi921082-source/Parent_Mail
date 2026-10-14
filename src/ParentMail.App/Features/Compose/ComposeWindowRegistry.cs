using System.Windows;

namespace ParentMail.App.Features.Compose;

/// <summary>
/// 開いている作成窓の一覧（終了時の確認など、アプリ全体から作成窓を数えるために使う）。
/// 窓が閉じると自動で外れる。UI スレッドからだけ使う。
/// 作成窓は同時に 1 つだけ（OpenOnce が守る）。一覧自体は汎用のまま（終了処理は全窓を順に閉じる）。
/// </summary>
public sealed class ComposeWindowRegistry
{
    private readonly List<ComposeWindow> _windows = new();

    public IReadOnlyList<ComposeWindow> Windows => _windows.ToList();
    public int Count => _windows.Count;

    /// <summary>窓が増えた・減ったとき。</summary>
    public event EventHandler? Changed;

    /// <summary>
    /// 開いている作成窓があれば、最小化を戻して前面に出し true を返す。無ければ false。
    /// </summary>
    public bool TryActivateExisting()
    {
        var existing = _windows.FirstOrDefault();
        if (existing is null) return false;
        if (existing.WindowState == WindowState.Minimized) existing.WindowState = WindowState.Normal;
        existing.Activate();
        return true;
    }

    /// <summary>
    /// 窓が 1 つも無いときだけ create で窓を作って登録・表示し true を返す。既にあれば前面に出すだけで false。
    /// 判定から登録までは UI スレッド上で途切れず進むので、続けて 2 回押されても窓は 1 つだけ。
    /// 作成や表示に失敗したら登録を残さず（次の要求を塞がない）、例外はそのまま投げる。
    /// </summary>
    public bool OpenOnce(Func<ComposeWindow> create)
    {
        if (TryActivateExisting()) return false;
        var window = create();
        Register(window);
        try
        {
            window.Show();
            window.Activate();
        }
        catch
        {
            Unregister(window);
            throw;
        }
        return true;
    }

    private void Unregister(ComposeWindow window)
    {
        if (_windows.Remove(window)) Changed?.Invoke(this, EventArgs.Empty);
    }

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
