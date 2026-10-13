namespace ParentMail.Core.Platform;

/// <summary>
/// アプリのデータ保存先（%LOCALAPPDATA%\Parent_Mail\）。
/// 日本語を含むユーザーフォルダでも動くよう、Path.Combine と UTF-16 文字列だけで組み立てる。
/// </summary>
public sealed class AppPaths
{
    public const string AppFolderName = "Parent_Mail";

    public AppPaths(string? dataRoot = null)
    {
        DataRoot = dataRoot ?? ResolveDefaultRoot();
    }

    /// <summary>テスト専用: データルートを上書きする環境変数（絶対パスのみ有効）。通常利用では設定しない。</summary>
    public const string DataRootEnvVar = "PARENTMAIL_DATA_ROOT";

    /// <summary>
    /// 既定のデータルート。環境変数 PARENTMAIL_DATA_ROOT が絶対パスならそれを使い、
    /// 空・相対パスなら無視して %LOCALAPPDATA%\Parent_Mail を返す。ルートを組み立てる箇所は全部ここを通す。
    /// </summary>
    public static string ResolveDefaultRoot()
    {
        var over = Environment.GetEnvironmentVariable(DataRootEnvVar);
        if (!string.IsNullOrWhiteSpace(over) && Path.IsPathFullyQualified(over))
            return over;
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolderName);
    }

    public string DataRoot { get; }
    public string LogsDir => Path.Combine(DataRoot, "logs");
    public string LogFile => Path.Combine(LogsDir, "parentmail.log");
    public string SettingsDb => Path.Combine(DataRoot, "settings.db");

    public string AccountDir(string accountId) => Path.Combine(DataRoot, "accounts", accountId);
    public string CacheDb(string accountId) => Path.Combine(AccountDir(accountId), "cache.db");
    public string AttachmentsDir(string accountId) => Path.Combine(AccountDir(accountId), "attachments");

    /// <summary>ログ用フォルダを作る（既にあれば何もしない）。</summary>
    public void EnsureLogsDir() => Directory.CreateDirectory(LogsDir);
}
