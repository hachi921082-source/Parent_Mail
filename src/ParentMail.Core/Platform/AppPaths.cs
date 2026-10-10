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
        DataRoot = dataRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolderName);
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
