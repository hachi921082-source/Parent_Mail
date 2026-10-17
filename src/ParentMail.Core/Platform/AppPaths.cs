namespace ParentMail.Core.Platform;

/// <summary>データルート解決が使うファイルシステム操作（テストで差し替える）。</summary>
public interface IDataRootFileSystem
{
    bool DirectoryExists(string path);

    /// <summary>フォルダを作り（無ければ）、.write-test を作成して削除できるか試す。失敗時は例外を投げる。</summary>
    void EnsureWritableDirectory(string path);
}

/// <summary>実際のファイルシステムを使う実装。</summary>
public sealed class RealDataRootFileSystem : IDataRootFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public void EnsureWritableDirectory(string path)
    {
        Directory.CreateDirectory(path);
        var probe = Path.Combine(path, ".write-test");
        using (File.Create(probe)) { }
        File.Delete(probe);
    }
}

/// <summary>データルートの解決結果。FallbackReason は Release で Data フォルダが使えず退避した時の原因。</summary>
public sealed record DataRootResolution(string Root, Exception? FallbackReason);

/// <summary>
/// アプリのデータ保存先。起動時に 1 回だけ解決する（Q93, A92-A98）。
/// 日本語を含むフォルダでも動くよう、Path.Combine と UTF-16 文字列だけで組み立てる。
/// </summary>
public sealed class AppPaths
{
    public const string AppFolderName = "Parent_Mail";
    public const string DataFolderName = "Data";

    /// <summary>データルートを上書きする環境変数（絶対パスのみ有効）。</summary>
    public const string DataRootEnvVar = "PARENTMAIL_DATA_ROOT";

#if DEBUG
    private const bool IsReleaseBuild = false;
#else
    private const bool IsReleaseBuild = true;
#endif

    private static readonly Lazy<AppPaths> CurrentLazy = new(CreateForProcess);

    /// <summary>この実行中プロセスで共通のデータパス（初回アクセス時に 1 回だけ解決）。</summary>
    public static AppPaths Current => CurrentLazy.Value;

    private static AppPaths CreateForProcess()
    {
        var exeFolder = Path.GetDirectoryName(Environment.ProcessPath);
        var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolderName);
        var r = Resolve(IsReleaseBuild, exeFolder, Environment.GetEnvironmentVariable(DataRootEnvVar), local, new RealDataRootFileSystem());
        return new AppPaths(r.Root, r.FallbackReason);
    }

    /// <summary>
    /// データルートの解決規則（純粋関数）。優先順: (1) 環境変数 (2) Release は exe 隣の Data
    /// （作れない・書けないなら localAppDataRoot に静かに退避）(3) Debug は既存の Data があればそれ、無ければ localAppDataRoot。
    /// </summary>
    public static DataRootResolution Resolve(bool isRelease, string? exeFolder, string? envValue,
        string localAppDataRoot, IDataRootFileSystem fs)
    {
        if (!string.IsNullOrWhiteSpace(envValue) && Path.IsPathFullyQualified(envValue))
            return new DataRootResolution(envValue, null);

        if (string.IsNullOrEmpty(exeFolder))
            return new DataRootResolution(localAppDataRoot, null);

        var data = Path.Combine(exeFolder, DataFolderName);
        if (isRelease)
        {
            try
            {
                fs.EnsureWritableDirectory(data);
                return new DataRootResolution(data, null);
            }
            catch (Exception ex)
            {
                return new DataRootResolution(localAppDataRoot, ex);
            }
        }

        return fs.DirectoryExists(data)
            ? new DataRootResolution(data, null)
            : new DataRootResolution(localAppDataRoot, null);
    }

    public AppPaths(string dataRoot, Exception? fallbackReason = null)
    {
        DataRoot = dataRoot;
        FallbackReason = fallbackReason;
    }

    /// <summary>Release で Data フォルダを使えず退避した場合の原因（無ければ null）。起動時に 1 度だけ Warn ログへ出す。</summary>
    public Exception? FallbackReason { get; }

    public string DataRoot { get; }
    public string SettingsDbPath => Path.Combine(DataRoot, "settings.db");
    public string LogsDir => Path.Combine(DataRoot, "logs");
    public string LogFile => Path.Combine(LogsDir, "parentmail.log");
    public string TempDir => Path.Combine(DataRoot, "temp");
    public string WebView2Dir => Path.Combine(DataRoot, "webview2");

    public string AccountDir(string accountId) => Path.Combine(DataRoot, "accounts", accountId);
    public string CacheDb(string accountId) => Path.Combine(AccountDir(accountId), "cache.db");
    public string AttachmentsDir(string accountId) => Path.Combine(AccountDir(accountId), "attachments");

    /// <summary>ログ用フォルダを作る（既にあれば何もしない）。</summary>
    public void EnsureLogsDir() => Directory.CreateDirectory(LogsDir);
}
