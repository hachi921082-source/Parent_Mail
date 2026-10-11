using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MimeKit;
using ParentMail.App.Resources;
using ParentMail.Core.Features.Compose;

namespace ParentMail.App.Features.Compose;

/// <summary>アドレス帳から選ぶ画面の 1 行。入力ずみの行はチェックされたまま、押せない。</summary>
public sealed partial class ContactAddressRow : ObservableObject
{
    public ContactAddressRow(PickerRow source)
    {
        Name = source.Name;
        Address = source.Address;
        AlreadyEntered = source.AlreadyEntered;
        _isChecked = source.AlreadyEntered;
    }

    public string Name { get; }
    public string Address { get; }
    public bool AlreadyEntered { get; }
    public bool IsEnabled => !AlreadyEntered;

    /// <summary>「入力ずみ」の印 [CP-21]。入力ずみでない行は空。</summary>
    public string TagText => AlreadyEntered ? Strings.CP_21 : "";

    [ObservableProperty]
    private bool _isChecked;

    /// <summary>押されたときの切り替え。入力ずみの行は変えない。</summary>
    public void Toggle()
    {
        if (!AlreadyEntered) IsChecked = !IsChecked;
    }
}

/// <summary>
/// アドレス帳から選ぶ画面の状態（compose PLAN-3, WD-31）。
/// 「入れる」で、新しくチェックされた行のアドレスが結果になる。1 つもなければエラー行を出して閉じない。
/// </summary>
public sealed class AddressPickerViewModel : ObservableObject
{
    private string _errorText = "";

    public AddressPickerViewModel(IReadOnlyList<PickerRow> rows, string accountName)
    {
        Rows = new ObservableCollection<ContactAddressRow>(rows.Select(r => new ContactAddressRow(r)));
        foreach (var row in Rows) row.PropertyChanged += (_, _) => ErrorText = "";
        Title = Strings.WD_31.Replace("<name>", accountName);
    }

    public string Title { get; }

    /// <summary>説明の行 [CP-19]。</summary>
    public string HintText => Strings.CP_19;

    public ObservableCollection<ContactAddressRow> Rows { get; }

    /// <summary>エラー行（1 行だけ）。空ならエラーなし。</summary>
    public string ErrorText
    {
        get => _errorText;
        private set => SetProperty(ref _errorText, value);
    }

    /// <summary>「入れる」で確定した結果（一覧の順）。</summary>
    public IReadOnlyList<MailboxAddress> Result { get; private set; } = Array.Empty<MailboxAddress>();

    /// <summary>「入れる」。新しくチェックされた行があれば結果にして true、なければエラー行を出して false。</summary>
    public bool TryConfirm()
    {
        var picked = Rows.Where(r => r.IsChecked && !r.AlreadyEntered)
            .Select(r => new MailboxAddress(string.IsNullOrWhiteSpace(r.Name) ? null : r.Name.Trim(), r.Address))
            .ToList();
        if (picked.Count == 0)
        {
            ErrorText = Strings.CP_20;
            return false;
        }
        Result = picked;
        return true;
    }
}
