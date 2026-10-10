using System.Text.RegularExpressions;

namespace ParentMail.Core.Features.AccountsAuth;

/// <summary>アカウント追加の入力欄の検査と既定値。</summary>
public static partial class AccountInputValidator
{
    // 空白なし・@ が 1 つ・ドメインにドットを含む、という最低限の形だけ見る
    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@.]+$")]
    private static partial Regex EmailPattern();

    public static bool IsValidEmail(string? email)
        => !string.IsNullOrWhiteSpace(email) && EmailPattern().IsMatch(email.Trim());

    /// <summary>表示名の既定値 = メールアドレスの @ より前。形が不正なら空。</summary>
    public static string DefaultDisplayName(string? email)
    {
        var trimmed = email?.Trim() ?? "";
        var at = trimmed.IndexOf('@');
        return at > 0 ? trimmed[..at] : "";
    }
}
