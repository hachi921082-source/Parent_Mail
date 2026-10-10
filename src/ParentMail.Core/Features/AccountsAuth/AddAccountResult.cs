namespace ParentMail.Core.Features.AccountsAuth;

/// <summary>アカウント追加の結果。LimitReached は PLAN の 4 値に加えた「2 つまで」の拒否。</summary>
public enum AddAccountResult { Ok, Duplicate, AuthFailed, NetworkError, LimitReached }
