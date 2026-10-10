namespace ParentMail.Core.Features.AddressBook;

// アドレス帳の画面を開く入口。一覧ペインのボタンと「送信者を登録」から使う。
public interface IAddressBookService
{
    /// <summary>アドレス帳ウィンドウをモーダルで開く。</summary>
    void OpenBook(string accountId);

    /// <summary>名前とアドレスを入れた登録ダイアログを開く。保存したら true。登録済みなら案内して false。</summary>
    bool AddFromSender(string accountId, string name, string address);
}
