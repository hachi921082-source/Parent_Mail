using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ParentMail.App.Resources;
using ParentMail.Core;
using ParentMail.Core.Data;
using ParentMail.Core.Features.AddressBook;
using ParentMail.Core.Models;

namespace ParentMail.App.Features.AddressBook;

/// <summary>編集ダイアログのメールアドレス 1 行。</summary>
public sealed partial class EmailRowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _text = "";
}

// 人の追加・編集ダイアログの状態と検証。保存まで（重複確認を含む）ここで行う。
public sealed partial class ContactEditViewModel : ObservableObject
{
    private readonly IContactRepository _repo;
    private readonly string _accountId;
    private readonly Contact? _existing;

    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _reading = "";

    [ObservableProperty]
    private string _memo = "";

    /// <summary>エラー行（1 行だけ表示）。空ならエラーなし。</summary>
    [ObservableProperty]
    private string _errorText = "";

    public ObservableCollection<EmailRowViewModel> Emails { get; } = new();

    /// <summary>保存に成功した連絡先（保存前は null）。</summary>
    public Contact? Result { get; private set; }

    public bool IsEdit => _existing is not null;
    public string Title => IsEdit ? AddressBookText.EditTitle : AddressBookText.AddTitle;

    /// <summary>existing があれば編集、なければ追加。追加のときは prefillName / prefillAddress を最初から入れられる。</summary>
    public ContactEditViewModel(IContactRepository repo, string accountId, Contact? existing = null,
        string? prefillName = null, string? prefillAddress = null)
    {
        _repo = repo;
        _accountId = accountId;
        _existing = existing;

        if (existing is not null)
        {
            Name = existing.Name;
            Reading = existing.Reading;
            Memo = existing.Memo;
            foreach (var e in existing.Emails) Emails.Add(new EmailRowViewModel { Text = e });
        }
        else
        {
            Name = prefillName ?? "";
            if (!string.IsNullOrEmpty(prefillAddress)) Emails.Add(new EmailRowViewModel { Text = prefillAddress });
        }
        if (Emails.Count == 0) Emails.Add(new EmailRowViewModel());
    }

    public void AddEmail() => Emails.Add(new EmailRowViewModel());

    /// <summary>行をはずす。最後の 1 行は消さずに空にする。</summary>
    public void RemoveEmail(EmailRowViewModel row)
    {
        if (Emails.Count <= 1)
        {
            row.Text = "";
            return;
        }
        Emails.Remove(row);
    }

    /// <summary>検証して保存する。エラーがあれば ErrorText に 1 件だけ入れて false。</summary>
    public bool TrySave()
    {
        ErrorText = "";

        var name = Name.Trim();
        if (name.Length == 0) return Fail(Strings.AB_07);

        var typed = Emails.Select(e => e.Text.Trim()).Where(t => t.Length > 0).ToList();
        if (typed.Count == 0) return Fail(Strings.AB_10);
        if (typed.Any(t => !ContactEmailValidator.IsValid(t))) return Fail(Strings.AB_09);

        // 同じアドレスを 2 行に入れた場合は 1 つにまとめる
        var seen = new HashSet<string>();
        var emails = typed.Where(t => seen.Add(TextNormalizer.NormalizeKey(t))).ToList();

        var ownId = _existing?.Id ?? 0;
        foreach (var e in emails)
        {
            var other = _repo.FindByEmail(_accountId, e);
            if (other is not null && other.Id != ownId) return Fail(Strings.AB_06);
        }

        var contact = new Contact(ownId, _accountId, name, Reading.Trim(), emails, Memo.Trim());
        try
        {
            if (_existing is null) contact = _repo.Add(contact);
            else _repo.Update(contact);
        }
        catch (DuplicateContactEmailException)
        {
            return Fail(Strings.AB_06);
        }
        Result = contact;
        return true;
    }

    private bool Fail(string message)
    {
        ErrorText = message;
        return false;
    }
}
