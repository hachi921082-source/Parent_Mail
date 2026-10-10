using ParentMail.App.Resources;
using ParentMail.Core;
using ParentMail.Core.SafetyWarnings;

namespace ParentMail.App.SafetyWarnings;

// 添付・転送で危険なファイルを断ったときの通知（SW-06、中身を確認できない zip は SW-07 を追加）。
public static class AttachBlockNotice
{
    public static void Show(IDialogService dialogs, ExecutableCheckResult result)
    {
        var message = result.Reason == ExecutableReason.UnreadableArchive
            ? Strings.SW_06 + "\n" + Strings.SW_07
            : Strings.SW_06;
        dialogs.ShowNotice(Strings.WD_10, message);
    }
}
