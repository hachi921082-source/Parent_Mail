namespace ParentMail.App.Features.AddressBook;

// 人の追加・編集ダイアログを出す窓口。画面を持たないテストでは差し替える。
public interface IContactEditPresenter
{
    /// <summary>ダイアログを出し、保存して閉じたら true、やめたら false。保存は ContactEditViewModel.TrySave が行う。</summary>
    bool ShowEdit(ContactEditViewModel vm);
}
