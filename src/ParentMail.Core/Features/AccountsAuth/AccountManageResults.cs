namespace ParentMail.Core.Features.AccountsAuth;

/// <summary>名前変更の結果。</summary>
public enum RenameResult { Ok, EmptyName, NotFound }

/// <summary>削除の結果。OutboxPending のときは何も消さない（送信待ちのメールが残っている）。</summary>
public enum DeleteResult { Ok, OutboxPending, NotFound }

/// <summary>パスワード入れ直しの結果。</summary>
public enum ReplacePasswordResult { Ok, EmptyPassword, AuthFailed, NetworkError, NotFound }
