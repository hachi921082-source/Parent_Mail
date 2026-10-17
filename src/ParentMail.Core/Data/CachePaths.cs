namespace ParentMail.Core.Data;

/// <summary>実行時データのパス規則（&lt;データルート&gt;\accounts\&lt;accountId&gt;\...）。日本語を含むパスも前提。</summary>
public sealed class CachePaths
{
    public string Root { get; }

    public CachePaths(string root)
    {
        if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("root が空", nameof(root));
        Root = root;
    }

    /// <summary>既定のデータルート（AppPaths が起動時に 1 回解決したもの）。</summary>
    public static string DefaultRoot()
        => ParentMail.Core.Platform.AppPaths.Current.DataRoot;

    public string AccountDir(string accountId)
    {
        // アカウント ID をそのままフォルダ名にするため、区切り文字や .. を含むものは拒否する
        if (string.IsNullOrEmpty(accountId) || accountId is "." or ".." || accountId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("アカウント ID が不正", nameof(accountId));
        return Path.Combine(Root, "accounts", accountId);
    }

    public string DbFile(string accountId) => Path.Combine(AccountDir(accountId), "cache.db");

    public string AttachmentsDir(string accountId) => Path.Combine(AccountDir(accountId), "attachments");

    /// <summary>cache.db を持つアカウントフォルダの ID 一覧。</summary>
    public IReadOnlyList<string> AccountIds()
    {
        var dir = Path.Combine(Root, "accounts");
        if (!Directory.Exists(dir)) return Array.Empty<string>();
        return Directory.EnumerateDirectories(dir)
            .Where(d => File.Exists(Path.Combine(d, "cache.db")))
            .Select(d => Path.GetFileName(d))
            .ToList();
    }
}
