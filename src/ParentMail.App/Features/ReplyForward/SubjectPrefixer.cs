using ParentMail.App.Resources;

namespace ParentMail.App.Features.ReplyForward;

/// <summary>題名に付ける接頭辞の種類。</summary>
public enum PrefixKind { Reply, Forward }

/// <summary>
/// 返信・転送の題名づくり（reply-forward PLAN 2 章、RF-01/02/07）。
/// すでに接頭辞が付いている題名には重ねて付けない（大文字小文字は区別しない）。
/// </summary>
public static class SubjectPrefixer
{
    private static readonly string[] ReplyMarks = { "Re:", "返信:" };
    private static readonly string[] ForwardMarks = { "Fwd:", "Fw:", "転送:" };

    public static string Add(PrefixKind kind, string? subject)
    {
        var s = subject ?? "";
        var marks = kind == PrefixKind.Reply ? ReplyMarks : ForwardMarks;
        var head = s.TrimStart();
        foreach (var mark in marks)
        {
            if (head.StartsWith(mark, StringComparison.OrdinalIgnoreCase)) return s;
        }
        var prefix = kind == PrefixKind.Reply ? Strings.RF_01 : Strings.RF_02;
        return prefix + " " + s;
    }
}
