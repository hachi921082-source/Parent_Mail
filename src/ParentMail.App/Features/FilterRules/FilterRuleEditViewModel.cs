using CommunityToolkit.Mvvm.ComponentModel;
using ParentMail.Core;
using ParentMail.Core.Models;

namespace ParentMail.App.Features.FilterRules;

/// <summary>動作の選択肢 1 つ（ドロップダウンの行）。</summary>
public sealed record ActionChoice(FilterAction Action, string Text);

// ルールの追加・編集ダイアログの状態と検証。保存まで（条件・動作の確認を含む）ここで行う。
// 保存ボタンは常に押せる。足りないときは赤いエラー行に理由を出す。
public sealed partial class FilterRuleEditViewModel : ObservableObject
{
    private readonly IFilterRepository _repo;
    private readonly string _accountId;
    private readonly FilterRule? _existing;

    public FilterRuleEditViewModel(IFilterRepository repo, string accountId, FilterRule? existing = null)
    {
        _repo = repo;
        _accountId = accountId;
        _existing = existing;

        // FR-02 の並び順で選択肢を作る
        Choices = new[] { FilterAction.MoveToSpam, FilterAction.MoveToTrash, FilterAction.MarkRead }
            .Select(a => new ActionChoice(a, FilterText.ActionText(a))).ToList();

        if (existing is not null)
        {
            SenderContains = existing.SenderContains ?? "";
            SubjectContains = existing.SubjectContains ?? "";
            BodyContains = existing.BodyContains ?? "";
            SelectedChoice = Choices.First(c => c.Action == existing.Action);
        }
    }

    public IReadOnlyList<ActionChoice> Choices { get; }

    public bool IsEdit => _existing is not null;
    public string Title => IsEdit ? FilterText.EditTitle : FilterText.AddTitle;

    [ObservableProperty]
    private string _senderContains = "";

    [ObservableProperty]
    private string _subjectContains = "";

    [ObservableProperty]
    private string _bodyContains = "";

    /// <summary>選ばれた動作。新しいルールでは最初は未選択。</summary>
    [ObservableProperty]
    private ActionChoice? _selectedChoice;

    [ObservableProperty]
    private string _errorText = "";

    /// <summary>保存に成功したルール（保存前は null）。</summary>
    public FilterRule? Result { get; private set; }

    /// <summary>検証して保存する。足りないものがあれば ErrorText に 1 件だけ入れて false。</summary>
    public bool TrySave()
    {
        ErrorText = "";
        var sender = Clean(SenderContains);
        var subject = Clean(SubjectContains);
        var body = Clean(BodyContains);

        if (sender is null && subject is null && body is null) return Fail(FilterText.NeedCondition);
        if (SelectedChoice is null) return Fail(FilterText.NeedAction);

        var rule = new FilterRule
        {
            Id = _existing?.Id ?? 0,
            AccountId = _accountId,
            SortOrder = _existing?.SortOrder ?? 0,
            Enabled = _existing?.Enabled ?? true,
            SenderContains = sender,
            SubjectContains = subject,
            BodyContains = body,
            Action = SelectedChoice.Action,
        };
        Result = _repo.Save(rule);
        return true;
    }

    private static string? Clean(string text)
    {
        var t = text.Trim();
        return t.Length == 0 ? null : t;
    }

    private bool Fail(string message)
    {
        ErrorText = message;
        return false;
    }
}
