using System.Runtime.InteropServices;
using System.Text;

namespace ParentMail.Core.Features.AccountsAuth;

/// <summary>
/// Windows 資格情報マネージャーにアプリパスワードを保管する ICredentialStore。
/// ターゲット名は ParentMail/&lt;accountId&gt;、保存先は「このユーザー・このパソコン」(CRED_PERSIST_LOCAL_MACHINE)。
/// パスワードは UTF-16 のバイト列で保存する。ログやファイルには一切出さない。
/// </summary>
public sealed class CredentialStore : ICredentialStore
{
    private const uint CredTypeGeneric = 1;
    private const uint CredPersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;
    // 資格情報マネージャーの 1 件の上限（バイト）
    private const int MaxBlobBytes = 5 * 512;

    public static string TargetName(string accountId) => "ParentMail/" + accountId;

    public void Save(string accountId, string appPassword)
    {
        var blob = Encoding.Unicode.GetBytes(appPassword);
        if (blob.Length > MaxBlobBytes) throw new ArgumentException("password too long", nameof(appPassword));

        var handle = GCHandle.Alloc(blob, GCHandleType.Pinned);
        try
        {
            var cred = new Credential
            {
                Type = CredTypeGeneric,
                TargetName = TargetName(accountId),
                CredentialBlobSize = (uint)blob.Length,
                CredentialBlob = handle.AddrOfPinnedObject(),
                Persist = CredPersistLocalMachine,
                UserName = "ParentMail",
            };
            if (!CredWriteW(ref cred, 0)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
        finally
        {
            handle.Free();
            Array.Clear(blob);
        }
    }

    public string? Get(string accountId)
    {
        if (!CredReadW(TargetName(accountId), CredTypeGeneric, 0, out var ptr))
        {
            var err = Marshal.GetLastWin32Error();
            if (err == ErrorNotFound) return null;
            throw new System.ComponentModel.Win32Exception(err);
        }
        try
        {
            var cred = Marshal.PtrToStructure<Credential>(ptr);
            if (cred.CredentialBlobSize == 0 || cred.CredentialBlob == IntPtr.Zero) return "";
            return Marshal.PtrToStringUni(cred.CredentialBlob, (int)cred.CredentialBlobSize / 2);
        }
        finally
        {
            CredFree(ptr);
        }
    }

    public void Delete(string accountId)
    {
        if (!CredDeleteW(TargetName(accountId), CredTypeGeneric, 0))
        {
            var err = Marshal.GetLastWin32Error();
            if (err != ErrorNotFound) throw new System.ComponentModel.Win32Exception(err);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string? UserName;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWriteW(ref Credential credential, uint flags);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredReadW(string target, uint type, uint flags, out IntPtr credential);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDeleteW(string target, uint type, uint flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);
}
