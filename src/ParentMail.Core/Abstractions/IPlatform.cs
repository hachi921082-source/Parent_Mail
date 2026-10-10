namespace ParentMail.Core;

// 基盤サービスの契約（資格情報・時計・ログ）。

/// <summary>アプリパスワードの保管（Windows 資格情報マネージャー、ターゲット名 ParentMail/&lt;accountId&gt;）。</summary>
public interface ICredentialStore
{
    void Save(string accountId, string appPassword);
    string? Get(string accountId);
    void Delete(string accountId);
}

/// <summary>現在時刻の取得（テストで差し替える）。</summary>
public interface IClock
{
    DateTime UtcNow { get; }
    DateTime LocalNow { get; }
}

/// <summary>ログ出力。メール本文・宛先・件名・パスワードは渡さない。</summary>
public interface IAppLog
{
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? ex = null);
}
