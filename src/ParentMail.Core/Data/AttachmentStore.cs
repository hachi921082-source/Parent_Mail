namespace ParentMail.Core.Data;

public sealed class AttachmentStore : IAttachmentStore
{
    private readonly CachePaths _paths;

    public AttachmentStore(CachePaths paths) => _paths = paths;

    public string GetFullPath(string accountId, long attachmentId)
        => Path.Combine(_paths.AttachmentsDir(accountId), attachmentId + ".bin");

    public string SaveFile(string accountId, long attachmentId, Stream content)
    {
        Directory.CreateDirectory(_paths.AttachmentsDir(accountId));
        var target = GetFullPath(accountId, attachmentId);
        // 途中で失敗した不完全なファイルを残さないよう、一時ファイルに書いてから置き換える
        var tmp = target + ".tmp";
        try
        {
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                content.CopyTo(fs);
            File.Move(tmp, target, overwrite: true);
        }
        catch
        {
            try { File.Delete(tmp); } catch (IOException) { }
            throw;
        }
        return Path.Combine("attachments", attachmentId + ".bin");
    }

    public Stream OpenRead(string accountId, long attachmentId)
        => new FileStream(GetFullPath(accountId, attachmentId), FileMode.Open, FileAccess.Read, FileShare.Read);

    public bool Exists(string accountId, long attachmentId) => File.Exists(GetFullPath(accountId, attachmentId));

    public void Delete(string accountId, long attachmentId)
    {
        var p = GetFullPath(accountId, attachmentId);
        if (File.Exists(p)) File.Delete(p);
    }
}
