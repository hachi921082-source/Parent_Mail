namespace ParentMail.Core.Data;

/// <summary>
/// 添付ファイルの実体の保管（accounts\&lt;id&gt;\attachments\&lt;attachmentId&gt;.bin）。
/// 元のファイル名は DB にだけ持ち、ここでは扱わない。ネットワークには触れない。
/// </summary>
public interface IAttachmentStore
{
    /// <summary>内容を保存し、アカウントフォルダからの相対パス（AttachmentInfo.LocalPath 用）を返す。</summary>
    string SaveFile(string accountId, long attachmentId, Stream content);
    /// <summary>保存済みファイルを読み取り用に開く。無ければ FileNotFoundException。</summary>
    Stream OpenRead(string accountId, long attachmentId);
    bool Exists(string accountId, long attachmentId);
    /// <summary>絶対パス（外部アプリで開く・保存先へコピーする用）。</summary>
    string GetFullPath(string accountId, long attachmentId);
    /// <summary>ファイルを消す。無ければ何もしない。</summary>
    void Delete(string accountId, long attachmentId);
}
