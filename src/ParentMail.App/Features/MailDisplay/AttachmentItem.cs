using CommunityToolkit.Mvvm.ComponentModel;
using ParentMail.App.Resources;
using ParentMail.Core.Features.MailDisplay;
using ParentMail.Core.Models;
using ParentMail.Core.SafetyWarnings;

namespace ParentMail.App.Features.MailDisplay;

/// <summary>
/// プレビュー下の添付一覧の 1 行（mail-display PLAN-3）。名前・大きさ・危険の印・処理中の状態を持つ。
/// 作成時はファイル名だけで判定する。zip は中身を見るため、ダウンロード後に <see cref="ApplyCheck"/> で判定を更新する。
/// </summary>
public sealed partial class AttachmentItem : ObservableObject
{
    public AttachmentItem(AttachmentInfo info)
    {
        Info = info;
        Id = info.Id;
        FileName = info.FileName;
        SizeText = AttachmentFileName.FormatSize(info.Size);
        ApplyCheck(ExecutableChecker.CheckName(info.FileName), contentChecked: false);
    }

    public AttachmentInfo Info { get; }
    public long Id { get; }
    public string FileName { get; }
    public string SizeText { get; }

    /// <summary>zip の中身まで確認済みか（zip 以外はファイル名の確認だけで済むので、作成時から確認済み扱い）。</summary>
    public bool ContentChecked { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanOpen), nameof(CanSave))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanOpen), nameof(DangerText), nameof(ReasonText), nameof(HasReason))]
    private bool _isDangerous;

    private ExecutableReason _reason;

    /// <summary>危険の印（赤い文言）。危険でなければ空。</summary>
    public string DangerText => IsDangerous ? Strings.PV_08 : "";

    /// <summary>中身を確認できない zip のときだけ出す理由の行。</summary>
    public string ReasonText => IsDangerous && _reason == ExecutableReason.UnreadableArchive ? Strings.SW_07 : "";
    public bool HasReason => ReasonText.Length > 0;

    /// <summary>危険なファイルは開けない。処理中も押せない。</summary>
    public bool CanOpen => !IsBusy && !IsDangerous;
    public bool CanSave => !IsBusy;

    /// <summary>zip かどうか（中身の確認が必要）。</summary>
    public bool NeedsContentCheck
        => !ContentChecked && FileName.TrimEnd().TrimEnd('.', ' ', '　').EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

    /// <summary>確認結果を反映する。</summary>
    public void ApplyCheck(ExecutableCheckResult result, bool contentChecked = true)
    {
        _reason = result.Reason;
        ContentChecked = contentChecked || !FileName.TrimEnd().TrimEnd('.', ' ', '　').EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        IsDangerous = result.Flagged;
        OnPropertyChanged(nameof(ReasonText));
        OnPropertyChanged(nameof(HasReason));
    }
}
