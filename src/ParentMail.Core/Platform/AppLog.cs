using System.Text;

namespace ParentMail.Core.Platform;

/// <summary>
/// ローカルログ（app-shell PLAN 3.5, PQ29）。1 行 1 件、1 MB で回転し 3 ファイル保持。
/// スレッドセーフで、書き込みに失敗しても例外を投げない。
/// </summary>
public sealed class AppLog : IAppLog
{
    public const long DefaultMaxBytes = 1024 * 1024;
    public const int FileCount = 3;

    private readonly string _logFile;
    private readonly long _maxBytes;
    private readonly Func<DateTime> _now;
    private readonly object _lock = new();

    public AppLog(AppPaths paths, long maxBytes = DefaultMaxBytes, Func<DateTime>? now = null)
    {
        _logFile = paths.LogFile;
        _maxBytes = maxBytes;
        _now = now ?? (() => DateTime.Now);
    }

    public void Info(string area, string message) => Write("INFO", area, message, null);
    public void Warn(string area, string message) => Write("WARN", area, message, null);
    public void Error(string area, string message, Exception? ex = null) => Write("ERROR", area, message, ex);

    private void Write(string level, string area, string message, Exception? ex)
    {
        try
        {
            var sb = new StringBuilder();
            sb.Append(_now().ToString("yyyy-MM-dd HH:mm:ss.fff")).Append(" [").Append(level).Append("] ")
              .Append(LogScrubber.Scrub(area)).Append(": ").Append(LogScrubber.Scrub(message));
            if (ex is not null)
            {
                // 種類・メッセージ・スタックだけを 1 行に収める。
                sb.Append(" | ").Append(ex.GetType().FullName).Append(": ").Append(LogScrubber.Scrub(ex.Message));
                if (ex.StackTrace is { } stack)
                {
                    sb.Append(" | ").Append(LogScrubber.Scrub(stack.Replace("\r", "").Replace('\n', ' ')));
                }
            }
            sb.Replace("\r", " ").Replace("\n", " ");
            sb.AppendLine();

            lock (_lock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_logFile)!);
                Rotate();
                File.AppendAllText(_logFile, sb.ToString(), new UTF8Encoding(false));
            }
        }
        catch
        {
            // ログの失敗でアプリを止めない。
        }
    }

    /// <summary>現在のログが上限以上なら、.2 を捨て、.1 を .2 に、現在を .1 に回す。</summary>
    internal void Rotate()
    {
        var info = new FileInfo(_logFile);
        if (!info.Exists || info.Length < _maxBytes) return;

        var dir = Path.GetDirectoryName(_logFile)!;
        var stem = Path.GetFileNameWithoutExtension(_logFile);
        var ext = Path.GetExtension(_logFile);
        string Rolled(int n) => Path.Combine(dir, $"{stem}.{n}{ext}");

        var last = Rolled(FileCount - 1);
        if (File.Exists(last)) File.Delete(last);
        for (var n = FileCount - 2; n >= 1; n--)
        {
            if (File.Exists(Rolled(n))) File.Move(Rolled(n), Rolled(n + 1));
        }
        File.Move(_logFile, Rolled(1));
    }
}
